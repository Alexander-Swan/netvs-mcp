using ModelContextProtocol.Protocol;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NetVsMcp.Broker.Services;

/// <summary>Public MCP projections. RPC contracts keep their full execution and inspection evidence.</summary>
internal static class DebuggingToolOutput
{
    private static readonly HashSet<string> Tools = new(("debug_status debug_get_mode debug_start debug_stop debug_continue debug_break debug_step " +
        "debug_restart debug_start_without_debugging debug_attach debug_get_callstack debug_select_frame debug_get_locals debug_evaluate " +
        "debug_snapshot debug_wait_for_break debug_eval_many debug_get_threads debug_set_variable debug_expand debug_exception debug_run_to debug_source_evidence " +
        "debug_hot_reload_apply watch_add watch_remove watch_list parallel_watch parallel_stacks thread_switch thread_set_frozen thread_get_callstack " +
        "breakpoint_set breakpoint_list breakpoint_remove breakpoint_enable breakpoint_group_list breakpoint_group_enable breakpoint_group_remove breakpoint_diagnostics " +
        "process_list_debugged process_list_local process_detach process_terminate immediate_execute exception_settings_get exception_settings_set module_list " +
        "dialog_list dialog_respond vs_events test_debug vs_context_snapshot vs_list_sessions vs_select_session vs_get_session get_status session_automation_control netvs_doctor " +
        "build_solution build_project build_status build_and_get_errors build_cancel clean_solution rebuild_solution build_configuration_get build_configuration_set " +
        "output_read output_write output_clear output_list_panes document_active document_read document_open document_list document_close open_relevant_files document_outline " +
        "code_document_symbols code_go_to_definition code_find_references find_implementations code_go_to_implementation code_workspace_symbols call_hierarchy_get symbol_context " +
        "editor_find find_in_files errors_list diagnostics_for_document diagnostics_binding_errors document_diagnostics selection_get selection_set editor_goto_line " +
        "edit_preview edit_approve edit_reject edit_list_pending code_actions_list code_actions_preview code_actions_apply code_actions_revert rename_symbol_preview rename_symbol_apply solution_overview window_list vs_launch_instance project_list project_dependencies project_add_file project_remove_file project_add_reference project_remove_reference package_restore test_discover test_run test_results_page test_run_and_get_results test_coverage_run test_coverage_results test_coverage_file vs_get_logs").Split(' '), StringComparer.Ordinal);

    internal static bool Handles(string? tool) => tool is not null && Tools.Contains(tool);

    internal static void Apply(CallToolResult result, string tool, string? solutionPath,
        IEnumerable<KeyValuePair<string, JsonElement>>? arguments = null)
    {
        if (!Handles(tool)) return;
        if (tool == "code_find_references") { ReferenceResponseOutput.Apply(result); return; }
        var args = arguments?.ToDictionary(p => p.Key, p => p.Value) ?? new();
        DebuggerResponseOutput.DeduplicateState(result);
        foreach (var text in result.Content.OfType<TextContentBlock>())
        {
            try
            {
                if (Project(JsonNode.Parse(text.Text), tool, solutionPath, args, out var output, out var error))
                {
                    text.Text = output?.ToJsonString() ?? "null";
                    if (error) result.IsError = true;
                }
            }
            catch (JsonException) { }
        }
        if (result.StructuredContent is { } structured && Project(JsonSerializer.SerializeToNode(structured), tool,
            solutionPath, args, out var projected, out var failed))
        {
            // MCP structuredContent must be an object. Scalars/arrays live only in content.
            result.StructuredContent = projected is JsonObject ? JsonSerializer.SerializeToElement(projected) : null;
            if (failed) result.IsError = true;
        }
    }

    private static bool Project(JsonNode? node, string tool, string? solutionPath, Dictionary<string, JsonElement> args,
        out JsonNode? output, out bool error)
    {
        output = null; error = false;
        if (node is not JsonObject envelope || envelope["success"] is not JsonValue success || !success.TryGetValue<bool>(out var ok)) return false;
        if (!ok)
        {
            output = Failure(envelope); Normalize(output, solutionPath, null); error = true; return true;
        }
        var value = envelope["value"]?.DeepClone();
        if (tool == "document_active" && String(value) is { } activePath)
            value = JsonValue.Create(BrokerToolService.CompactSourcePath(activePath, solutionPath));
        if (value is JsonObject operation && (IsFalse(operation["supported"]) || IsFalse(operation["success"])))
        {
            output = Failure(operation); Normalize(output, solutionPath, null); error = true; return true;
        }
        if (tool == "vs_list_sessions" && value is JsonArray sessionList)
            foreach (var session in sessionList.OfType<JsonObject>()) Normalize(session, String(session["solutionPath"]), null);
        else Normalize(value, solutionPath, null, tool is "test_coverage_run" or "test_coverage_results" or "test_coverage_file");
        if (tool == "rename_symbol_preview" && value is JsonObject preview && preview["symbol"] is null)
        {
            output = Failure(preview);
            error = true;
            return true;
        }
        output = Shape(value, tool, args);
        // Do not discard recovery/routing information carried outside the value.
        if (envelope["metadata"] is { } metadata)
            output = output is JsonObject obj && !obj.ContainsKey("metadata") ? With(obj, "metadata", metadata.DeepClone()) :
                new JsonObject { ["result"] = output, ["metadata"] = metadata.DeepClone() };
        return true;
    }

    private static JsonObject Failure(JsonObject source)
    {
        var result = (JsonObject)source.DeepClone();
        result.Remove("success"); result.Remove("supported");
        result["error"] = result["message"]?.DeepClone() ?? JsonValue.Create("The operation failed or is unavailable.");
        result.Remove("message");
        if (result["value"] is null) result.Remove("value");
        return result;
    }

    private static JsonObject With(JsonObject obj, string key, JsonNode? value) { obj[key] = value; return obj; }
    private static bool IsFalse(JsonNode? node) => node is JsonValue v && v.TryGetValue<bool>(out var value) && !value;
    private static bool IsTrue(JsonNode? node) => node is JsonValue v && v.TryGetValue<bool>(out var value) && value;
    private static string? String(JsonNode? node) => node is JsonValue v && v.TryGetValue<string>(out var value) ? value : null;
    private static JsonNode? Copy(JsonNode? node) => node?.DeepClone();
    private static void Remove(JsonObject obj, params string[] keys) { foreach (var key in keys) obj.Remove(key); }

    private static void Normalize(JsonNode? node, string? solutionPath, JsonObject? state, bool preservePaths = false)
    {
        if (node is JsonArray array) { foreach (var item in array) Normalize(item, solutionPath, state, preservePaths); return; }
        if (node is not JsonObject obj) return;
        state = obj["state"] as JsonObject ?? state;
        foreach (var (key, value) in obj.ToArray())
        {
            // Text, identifiers and arbitrary evaluator values are never rewritten as paths.
            // Coverage paths are retained-history keys and must round-trip unchanged.
            if (!preservePaths && key is ("file" or "path" or "documentPath" or "activeDocument" or "symbolFile" or "requestedFile" or "hitFile" or "projectPath") && String(value) is { } path)
                obj[key] = BrokerToolService.CompactSourcePath(path, solutionPath);
            else Normalize(value, solutionPath, state, preservePaths);
        }
        if (obj["eventCursor"] is JsonObject cursor && obj["sessionId"] is not null && JsonNode.DeepEquals(obj["sessionId"], cursor["sessionId"])) cursor.Remove("sessionId");
        if (obj["context"] is JsonObject context && state is not null)
            foreach (var key in new[] { "stopId", "generation" })
                if (JsonNode.DeepEquals(context[key], state[key])) context.Remove(key);
        if ((String(obj["file"]) ?? String(obj["path"])) is { } file && obj["line"] is not null)
        {
            obj["location"] = $"{file}:{obj["line"]}:{obj["column"] ?? JsonValue.Create(0)}";
            Remove(obj, "file", "path", "line", "column");
        }
        if (obj["kind"] is not null && obj["name"] is not null && (obj.ContainsKey("containingNamespace") || obj.ContainsKey("containingType")))
        {
            var container = String(obj["containingType"]) ?? String(obj["containingNamespace"]);
            var ns = String(obj["containingNamespace"]);
            if (!string.IsNullOrEmpty(ns) && !string.IsNullOrEmpty(container) && container != ns && !container.StartsWith(ns + ".", StringComparison.Ordinal))
                container = ns + "." + container;
            if (!string.IsNullOrEmpty(container)) obj["name"] = container + "." + String(obj["name"]);
            Remove(obj, "containingType", "containingNamespace");
        }
        if (obj["path"] is not null && String(obj["name"]) is { } name &&
            string.Equals(Path.GetFileName(String(obj["path"])?.Replace('/', Path.DirectorySeparatorChar)), name, StringComparison.OrdinalIgnoreCase)) obj.Remove("name");
        foreach (var key in new[] { "success", "supported", "isEnabled" }) if (IsTrue(obj[key])) obj.Remove(key);
        foreach (var key in new[] { "isCurrent", "hasMore", "timedOut", "truncated", "resultsTruncated", "evidenceTruncated", "locationsTruncated", "errorNotificationsTruncated", "messageTruncated", "continueAfterAction", "underlyingModeIsStale", "userDefined", "isImplicit", "isBeingDebugged" })
            if (IsFalse(obj[key])) obj.Remove(key);
        foreach (var key in obj.Where(p => p.Value is null).Select(p => p.Key).ToArray()) obj.Remove(key);
    }

    private static JsonNode? Shape(JsonNode? node, string tool, Dictionary<string, JsonElement> args)
    {
        if (node is not JsonObject obj)
        {
            if (tool == "vs_list_sessions" && node is JsonArray sessions) foreach (var session in sessions.OfType<JsonObject>()) Session(session);
            return node;
        }
        switch (tool)
        {
            case "debug_snapshot": case "debug_wait_for_break":
                (obj["state"] as JsonObject)?.Remove("sessionId");
                foreach (var (key, kind) in new[] { ("locals", "debug_get_locals"), ("watch", "watch_list"), ("threads", "debug_get_threads"),
                    ("parallelStacks", "parallel_stacks"), ("parallelWatch", "parallel_watch"), ("breakpoints", "breakpoint_list"), ("modules", "module_list") })
                    if (obj[key] is JsonObject child)
                        obj[key] = key == "locals" ? Expressions((JsonObject)child.DeepClone(), "locals") : Shape(child.DeepClone(), kind, args);
                if (obj["context"] is JsonObject ctx && obj["callStack"]?["frames"] is JsonArray frames &&
                    frames.OfType<JsonObject>().Any(f => JsonNode.DeepEquals(f["frameIndex"], ctx["frameIndex"])))
                    Remove(ctx, "functionName", "location", "frameDepth");
                if (obj["callStack"] is JsonObject stack) CompactFrames(stack);
                foreach (var key in new[] { "threads", "breakpoints" })
                    if (obj[key] is JsonObject section && section.Count == 1 && section[key] is JsonArray rows)
                        obj[key] = rows.DeepClone();
                return obj;
            case "debug_get_locals":
                if (String(obj["state"]?["mode"]) == "dbgBreakMode") Remove(obj, "state", "context");
                return Expressions(obj, "locals");
            case "debug_get_callstack": case "thread_get_callstack":
                if (String(obj["state"]?["mode"]) == "dbgBreakMode") Remove(obj, "state", "context");
                CompactFrames(obj);
                return obj;
            case "watch_list": return Expressions(obj, "watches");
            case "parallel_watch": return Expressions(obj, "expressions");
            case "debug_evaluate":
                if (obj["expression"] is JsonObject expression) obj["expression"] = ExpressionMap(new JsonArray(expression.DeepClone()));
                return obj;
            case "debug_eval_many":
                if (String(obj["state"]?["mode"]) == "dbgBreakMode") Remove(obj, "state", "context");
                if (obj["results"] is JsonArray results && results.All(r => r is JsonObject row && row.Count == 1 && row["expression"] is JsonObject))
                    obj["results"] = ExpressionMap(new JsonArray(results.Select(r => Copy(r!["expression"])).ToArray()));
                return obj;
            case "debug_expand": return Expansion(obj);
            case "debug_select_frame":
                if (obj["frame"] is not JsonObject selected) return obj;
                var selectedFrame = (JsonObject)selected.DeepClone();
                if (selectedFrame["frameIndex"] is null) selectedFrame["frameIndex"] = Copy(obj["frameIndex"]);
                selectedFrame.Remove("isCurrent");
                CompactFrame(selectedFrame); return selectedFrame;
            case "debug_set_variable": return obj["evaluation"]?["expression"] is JsonObject evaluated ? ExpressionValue(evaluated) :
                new JsonObject { ["$unavailable"] = "Assignment completed, but the resulting value could not be read." };
            case "watch_add":
                var watchValue = obj["watch"] is JsonObject watch ? ExpressionValue(watch, true) : new JsonObject { ["$unavailable"] = "Watch added; no value is available." };
                if (watchValue is JsonObject unavailable && unavailable.ContainsKey("$unavailable") && unavailable["$unavailable"] is null)
                    unavailable["$unavailable"] = String(obj["message"]) ?? "Watch added; no value is available.";
                return obj["context"] is { } watchContext ? new JsonObject { ["$value"] = watchValue, ["$context"] = watchContext.DeepClone() } : watchValue;
            case "watch_remove": case "thread_switch": case "thread_set_frozen": case "process_detach": case "process_terminate":
            case "exception_settings_set": case "debug_hot_reload_apply": case "build_configuration_set": case "output_write": case "output_clear":
            case "document_close": case "selection_set": case "editor_goto_line": case "vs_select_session":
                return NeedsObservation(obj) ? obj : JsonValue.Create(true);
            case "dialog_respond": return obj.ContainsKey("operation") || HasDiagnostics(obj) ? obj : JsonValue.Create(true);
            case "debug_get_mode": return String(obj["mode"]) == "dbgModalBlock" ? obj : Copy(obj["mode"]);
            case "debug_start_without_debugging":
                return obj["launchedProcessIds"] is JsonArray ids && ids.Count > 0
                    ? ids.Count == 1 ? Copy(ids[0]) : ids.DeepClone()
                    : new JsonObject { ["launched"] = true, ["processIdUnavailable"] = true };
            case "debug_get_threads": return Collection(obj, "threads", false);
            case "process_list_debugged": case "process_list_local":
                if (obj["processes"] is JsonArray processes) foreach (var p in processes.OfType<JsonObject>())
                {
                    Rename(p, "processId", "id");
                    if (tool == "process_list_local") p.Remove("transport");
                }
                return Collection(obj, "processes", true);
            case "debug_attach": if (obj["process"] is JsonObject process) Rename(process, "processId", "id"); return obj;
            case "parallel_stacks": return ParallelStacks(obj);
            case "breakpoint_set": return Copy(obj["name"]) ?? obj;
            case "breakpoint_list":
                if (obj["breakpoints"] is JsonArray breakpoints) foreach (var b in breakpoints.OfType<JsonObject>()) Breakpoint(b);
                return Collection(obj, "breakpoints", true);
            case "breakpoint_group_list": return BreakpointGroups(obj);
            case "breakpoint_remove": return JsonValue.Create((obj["removed"]?.GetValue<int>() ?? 0) > 0);
            case "breakpoint_enable":
                if (NeedsObservation(obj)) return Select(obj, "updated", "state", "message", "operation");
                if (!RequestedContinue(args) || obj["state"] is null) return JsonValue.Create((obj["updated"]?.GetValue<int>() ?? 0) > 0);
                var toggle = Select(obj, "updated");
                toggle["resumeOutcome"] = Copy(obj["state"]?["outcome"]) ?? Copy(obj["state"]?["mode"]);
                return toggle;
            case "breakpoint_group_enable":
                var groupNeedsObservation = NeedsObservation(obj);
                if (!groupNeedsObservation && obj["state"] is JsonObject groupState) Remove(groupState, "mode", "outcome", "sessionId");
                return RequestedContinue(args) || groupNeedsObservation ? Select(obj, "matched", "updated", "state", "message") : Select(obj, "matched", "updated");
            case "breakpoint_group_remove": return Copy(obj["updated"]) ?? Copy(obj["removed"]) ?? obj;
            case "breakpoint_diagnostics":
                if (obj["errorNotifications"] is JsonArray notifications)
                    foreach (var notification in notifications.OfType<JsonObject>()) Remove(notification, "observedUtc", "engineId");
                if (obj["breakpoints"] is JsonArray bindingEvidence)
                    foreach (var binding in bindingEvidence.OfType<JsonObject>())
                    {
                        if (binding["name"] is not null) binding.Remove("location");
                        if (IsTrue(binding["enabled"])) binding.Remove("enabled");
                    }
                return obj;
            case "module_list": return Modules(obj, args);
            case "immediate_execute": return Copy(obj["output"]) ?? JsonValue.Create("");
            case "exception_settings_get": return ExceptionSettings(obj);
            case "dialog_list": return Collection(obj, "dialogs", true);
            case "vs_events": return Events(obj);
            case "debug_run_to":
                Coordinates(obj, "requested"); Coordinates(obj, "hit");
                if (String(obj["outcome"]) is "reachedLocation" or "alreadyAtLocation" && IsTrue(obj["cleanup"]?["complete"]))
                {
                    var hit = Select(obj, "outcome", "hitLocation");
                    if (obj["state"] is JsonObject hitState) hit["state"] = Select(hitState, "generation", "stopId");
                    return hit;
                }
                Remove(obj, "startedGeneration", "startedStopId", "sourceMatchStatus");
                return obj;
            case "debug_source_evidence":
                var evidence = Select(obj, "location", "message");
                if (args.TryGetValue("includeChecksums", out var checksums) && checksums.ValueKind == JsonValueKind.True)
                {
                    Merge(evidence, Select(obj, "checksumAlgorithm", "expectedChecksums", "disk", "editor"));
                    return evidence;
                }
                foreach (var key in new[] { "disk", "editor" })
                    if (obj[key] is JsonObject comparison)
                        evidence[key] = comparison.ContainsKey("message") ? Select(comparison, "status", "message") : Copy(comparison["status"]);
                return evidence;
            case "test_debug": return TestDebug(obj);
            case "vs_context_snapshot": return Context(obj);
            case "get_status": Session(obj); return obj;
            case "vs_get_session": if (obj["session"] is JsonObject session) Session(session); return obj;
            case "session_automation_control": return args.ContainsKey("paused") && obj["activeMethod"] is null ? JsonValue.Create(true) : Select(obj, "paused", "activeMethod");
            case "netvs_doctor": return Doctor(obj);
            case "build_solution": case "build_project": case "clean_solution": case "rebuild_solution": return Build(obj, false);
            case "build_status": case "build_cancel": return Build(obj, true);
            case "build_and_get_errors":
                if (obj["build"] is JsonObject build)
                {
                    var buildSummary = (JsonObject)Build(build, false);
                    if (String(buildSummary["outcome"]) is "completed" or "failed") buildSummary.Remove("message");
                    obj["build"] = buildSummary;
                }
                if (obj["errors"] is JsonObject errors)
                {
                    obj.Remove("errors");
                    var diagnostics = Diagnostics(errors);
                    Remove(diagnostics, "source", "capturedUtc");
                    Merge(obj, diagnostics);
                }
                return obj;
            case "document_diagnostics": return DocumentDiagnostics(obj);
            case "errors_list": case "diagnostics_for_document": case "diagnostics_binding_errors": return Diagnostics(obj);
            case "output_read": if (args.ContainsKey("paneName")) obj.Remove("paneName"); return obj;
            case "output_list_panes": return Names(obj, "panes", "name");
            case "document_read":
                if (obj["document"] is JsonObject document) { obj["path"] = Copy(document["path"]); obj.Remove("document"); }
                Remove(obj, "usedLiveBuffer", "textVersion"); return obj;
            case "document_open": return Copy(obj["path"]) ?? obj;
            case "document_list": return Documents(obj);
            case "project_list": return Projects(obj["projects"]);
            case "solution_overview":
                if (obj["projects"] is JsonObject projectList) obj["projects"] = Projects(projectList["projects"]);
                if (obj["testProjects"] is JsonArray testProjects) obj["testProjects"] = Projects(testProjects);
                return obj;
            case "project_dependencies":
                obj.Remove("project");
                if (obj["projectReferences"] is JsonArray projectReferences)
                    obj["projectReferences"] = new JsonArray(projectReferences.OfType<JsonObject>().Select(reference => Copy(reference["path"])).ToArray());
                return obj;
            case "window_list":
                if (obj["windows"] is JsonArray windows)
                    foreach (var window in windows.OfType<JsonObject>())
                    {
                        if (!IsTrue(window["isActive"])) window.Remove("isActive");
                        if (IsTrue(window["isVisible"])) window.Remove("isVisible");
                        if (!args.TryGetValue("includeObjectKind", out var includeKind) || includeKind.ValueKind != JsonValueKind.True)
                            window.Remove("objectKind");
                    }
                return Collection(obj, "windows", true);
            case "vs_launch_instance": return Copy(obj["session"]?["sessionId"]) ?? obj;
            case "edit_approve": return Copy(obj["mutation"]?["saved"]) ?? obj;
            case "edit_reject": case "project_add_file": case "project_remove_file":
            case "project_add_reference": case "project_remove_reference": return JsonValue.Create(true);
            case "package_restore": return JsonValue.Create(true);
            case "edit_preview":
                if (obj["pendingEdit"] is not JsonObject pendingEdit) return obj;
                CompactRange(pendingEdit);
                return Select(pendingEdit, "editId", "path", "start", "end");
            case "edit_list_pending":
                if (obj["pendingEdits"] is not JsonArray edits) return obj;
                return new JsonArray(edits.OfType<JsonObject>().Select(edit =>
                {
                    CompactRange(edit);
                    return Select(edit, "editId", "operation", "path", "start", "end");
                }).ToArray());
            case "code_actions_list": return CodeActions(obj);
            case "code_actions_preview": case "rename_symbol_preview":
                return Preview(obj, tool == "code_actions_preview");
            case "code_actions_apply": case "code_actions_revert": case "rename_symbol_apply":
                return AppliedAction(obj, tool == "code_actions_revert");
            case "open_relevant_files": return Names(obj, "documents", "path");
            case "document_outline": return Collection(obj, "symbols", true, "documentPath");
            case "code_document_symbols": return Collection(obj, "symbols", true);
            case "code_workspace_symbols": return IsTrue(obj["truncated"]) ? Collection(obj, "symbols", true, "query") : Collection(obj, "symbols", true, "query", "matchCount");
            case "code_go_to_definition": return Collection((JsonObject)Navigation(obj), "definitions", true, "symbol", "navigated");
            case "find_implementations": case "code_go_to_implementation": return Navigation(obj);
            case "symbol_context": return SymbolContext(obj);
            case "call_hierarchy_get":
                Remove(obj, "direction");
                if (obj["symbol"] is not null) obj.Remove("message");
                return Hierarchy(obj);
            case "editor_find": return Search(obj, singleFile: true, args);
            case "find_in_files": return Search(obj);
            case "selection_get": return Selection(obj);
            case "test_discover":
                if (obj["results"] is JsonArray noResults && noResults.Count == 0) obj.Remove("results");
                return Collection(obj, "tests", true, "message", "backend");
            case "test_run": return TestRun(obj);
            case "test_results_page":
                if (String(obj["outcome"]) is "running" or "cancelRequested") return Select(obj, "outcome");
                if (obj["results"] is JsonArray testRows)
                    foreach (var row in testRows.OfType<JsonObject>()) Remove(row, "executionId", "caseId");
                var page = Select(obj, "results", "nextOffset", "resultsTruncated");
                if (obj["results"] is JsonArray { Count: 0 } && String(obj["outcome"]) is not "completed")
                    Merge(page, Select(obj, "outcome"));
                return page;
            case "test_run_and_get_results": return TestComposite(obj);
            case "test_coverage_run": case "test_coverage_results":
                if (obj["coverage"] is JsonObject coverage)
                {
                    coverage.Remove("runId");
                    if (coverage["files"] is JsonArray files)
                        foreach (var file in files.OfType<JsonObject>())
                            if (file["lines"] is JsonArray { Count: 0 }) file.Remove("lines");
                }
                return Coverage(obj);
            case "test_coverage_file": return Coverage(obj);
            case "vs_get_logs": return Collection(obj, "files", true, "logsDirectory");
            default: return obj;
        }
    }

    private static void CompactFrame(JsonObject frame)
    {
        Rename(frame, "frameIndex", "index");
        Rename(frame, "functionName", "function");
        if (frame["line"]?.ToJsonString() == "0") frame.Remove("line");
        if (frame["column"]?.ToJsonString() == "0") frame.Remove("column");
    }

    private static void CompactFrames(JsonObject stack)
    {
        if (stack["frames"] is JsonArray frames)
            foreach (var frame in frames.OfType<JsonObject>()) CompactFrame(frame);
    }

    private static void CompactRange(JsonObject edit)
    {
        foreach (var endpoint in new[] { "start", "end" })
        {
            if (edit[endpoint + "Line"] is { } line && edit[endpoint + "Column"] is { } column)
                edit[endpoint] = $"{line}:{column}";
            Remove(edit, endpoint + "Line", endpoint + "Column");
        }
    }

    private static JsonNode Documents(JsonObject obj)
    {
        if (obj["documents"] is not JsonArray documents) return obj;
        var active = String(obj["activeDocument"]);
        return new JsonArray(documents.OfType<JsonObject>().Select(document =>
        {
            var row = document["path"] is null ? Select(document, "name") : Select(document, "path");
            var path = String(document["path"]) ?? String(document["name"]);
            if (path is not null && string.Equals(active, path, StringComparison.OrdinalIgnoreCase)) row["active"] = true;
            if (IsFalse(document["isSaved"])) row["unsaved"] = true;
            return row;
        }).ToArray());
    }

    private static JsonArray Projects(JsonNode? node)
    {
        var projects = new JsonArray();
        if (node is not JsonArray rows) return projects;
        foreach (var project in rows.OfType<JsonObject>())
        {
            var row = Select(project, "name");
            var path = String(project["uniqueName"]) ?? String(project["fullName"]);
            if (path is not null) row["path"] = path.Replace('\\', '/');
            if (IsFalse(project["isLoaded"])) row["loaded"] = false;
            projects.Add(row);
        }
        return projects;
    }

    private static JsonNode CodeActions(JsonObject obj)
    {
        if (obj["actions"] is not JsonArray actions) return obj;
        var rows = new JsonArray(actions.OfType<JsonObject>().Select(action =>
            Select(action, action["actionId"] is null ? "index" : "actionId", "title", "kind", "diagnosticId")).ToArray());
        if (obj["discovery"] is JsonObject discovery)
        {
            var evidence = Select(discovery, "truncated");
            if (discovery["providerErrors"] is JsonArray { Count: > 0 } errors) evidence["providerErrors"] = errors.DeepClone();
            if (evidence.Count > 0)
            {
                evidence["actions"] = rows;
                return evidence;
            }
        }
        return rows;
    }

    private static JsonNode Preview(JsonObject obj, bool includeTitle)
    {
        var preview = new JsonObject();
        if (includeTitle && obj["appliedTitle"] is { } title) preview["title"] = title.DeepClone();
        var files = new JsonObject();
        if (obj["changes"] is JsonArray changes)
            foreach (var change in changes.OfType<JsonObject>())
            {
                var path = String(change["file"]) ?? string.Empty;
                if (files[path] is not JsonArray edits) files[path] = edits = new JsonArray();
                CompactRange(change);
                edits.Add(Select(change, "start", "end", "oldText", "newText"));
            }
        preview["changes"] = files;
        return preview;
    }

    private static JsonNode AppliedAction(JsonObject obj, bool revert)
    {
        if (String(obj["outcome"]) is "unknown" or "failedWithChanges" or "notApplied") return obj;
        var result = Select(obj, "recoveryId");
        result["outcome"] = revert ? "reverted" : "applied";
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in new[] { "changes", "documentVersions" })
            if (obj[key] is JsonArray rows)
                foreach (var row in rows.OfType<JsonObject>())
                    if (String(row["file"]) is { } path) files.Add(path);
        result["files"] = new JsonArray(files.Select(path => (JsonNode?)JsonValue.Create(path)).ToArray());
        return result;
    }

    private static JsonObject Select(JsonObject obj, params string[] keys)
    {
        var result = new JsonObject(); foreach (var key in keys) if (obj[key] is { } value) result[key] = value.DeepClone(); return result;
    }
    private static void Rename(JsonObject obj, string from, string to) { if (obj[from] is { } value) { obj[to] = value.DeepClone(); obj.Remove(from); } }
    private static void Merge(JsonObject target, JsonObject source) { foreach (var (key, value) in source) target[key] = Copy(value); }
    private static bool HasDiagnostics(JsonObject obj) => obj.ContainsKey("message") || obj.ContainsKey("state") || obj.ContainsKey("context") || obj.ContainsKey("operation") || IsFalse(obj["supported"]);
    private static bool NeedsObservation(JsonObject obj) => obj.ContainsKey("operation") ||
        String(obj["state"]?["mode"]) == "dbgModalBlock" || String(obj["state"]?["outcome"]) is "pending" or "dialogBlocked" or "timedOut";
    private static bool RequestedContinue(Dictionary<string, JsonElement> args)
        => args.TryGetValue("continueExecution", out var value) && value.ValueKind == JsonValueKind.True;

    private static JsonNode Collection(JsonObject obj, string key, bool unwrap, params string[] discard)
    {
        Remove(obj, discard);
        if (unwrap && obj.Count == 1 && obj[key] is { } value) return value.DeepClone();
        return obj;
    }
    private static JsonNode Names(JsonObject obj, string key, string name)
    {
        if (obj[key] is JsonArray array) obj[key] = new JsonArray(array.Select(v => Copy(v?[name])).ToArray());
        return Collection(obj, key, true);
    }
    private static JsonNode? ExpressionValue(JsonObject expression, bool unavailable = false)
    {
        var value = Copy(expression["value"]);
        if (IsFalse(expression["isValidValue"])) return new JsonObject { [unavailable ? "$unavailable" : "$error"] = value };
        var text = String(value);
        return text is not null && (text.StartsWith('{') && text.EndsWith('}') || text.StartsWith("Count = ", StringComparison.Ordinal))
            ? new JsonObject { ["$preview"] = value } : value;
    }
    private static JsonNode ExpressionMap(JsonArray array)
    {
        var map = new JsonObject();
        foreach (var item in array)
        {
            if (item is not JsonObject e || String(e["name"]) is not { Length: > 0 } name || map.ContainsKey(name)) return array.DeepClone();
            map[name] = ExpressionValue(e);
        }
        return map;
    }
    private static JsonNode Expressions(JsonObject obj, string key)
    {
        if (obj[key] is JsonArray values) obj[key] = ExpressionMap(values);
        return obj.Count == 1 && obj[key] is { } valuesOnly ? valuesOnly.DeepClone() : obj;
    }
    private static JsonNode? Expansion(JsonObject obj)
    {
        if (obj["root"] is not JsonObject root) return obj;
        var value = ExpandNode(root, out var truncated);
        // Preserve continuation and budget evidence only when the expansion is incomplete.
        if (truncated)
        {
            var continuations = new JsonArray();
            void Collect(JsonObject current)
            {
                if (current.ContainsKey("nextOffset") || current.ContainsKey("truncationReason"))
                    continuations.Add(Select(current, "memberPath", "nextOffset", "truncationReason"));
                if (current["children"] is JsonArray children) foreach (var child in children.OfType<JsonObject>()) Collect(child);
            }
            Collect(root);
            var evidence = Select(obj, "visitedNodes", "elapsedMilliseconds", "allowFunctionEvaluation", "context");
            if (continuations.Count > 0) evidence["continuations"] = continuations;
            else evidence["ambiguousMembers"] = true;
            return new JsonObject { ["$value"] = value, ["$expansion"] = evidence };
        }
        return obj["context"] is { } context ? new JsonObject { ["$value"] = value, ["$context"] = context.DeepClone() } : value;
    }
    private static JsonNode? ExpandNode(JsonObject node, out bool truncated)
    {
        truncated = node.ContainsKey("nextOffset") || node.ContainsKey("truncationReason");
        if (IsFalse(node["isValid"])) return new JsonObject { ["$error"] = Copy(node["value"]) };
        if (node["children"] is not JsonArray children || children.Count == 0)
            return String(node["value"]) is { } text && (text.StartsWith('{') || text.StartsWith("Count = ", StringComparison.Ordinal))
                ? new JsonObject { ["$preview"] = text } : Copy(node["value"]);
        var map = new JsonObject();
        foreach (var child in children.OfType<JsonObject>())
        {
            var name = String(child["name"]);
            if (string.IsNullOrEmpty(name) || map.ContainsKey(name)) { truncated = true; return node.DeepClone(); }
            map[name] = ExpandNode(child, out var childTruncated); truncated |= childTruncated;
        }
        return map;
    }
    private static void Breakpoint(JsonObject b)
    {
        if (b["name"] is not null && JsonNode.DeepEquals(b["name"], b["location"])) b.Remove("location");
        if (b["name"] is not null && JsonNode.DeepEquals(b["name"], b["functionName"])) b.Remove("functionName");
        CompactFrame(b);
        if (IsTrue(b["enabled"])) b.Remove("enabled");
        if (String(b["action"]) == "Break") b.Remove("action");
    }
    private static JsonNode BreakpointGroups(JsonObject obj)
    {
        if (obj["breakpoints"] is not JsonArray items) return obj;
        var groups = new JsonObject();
        foreach (var group in (obj["groups"] as JsonArray ?? []).Select(String).Where(g => g is not null)) groups[group!] = new JsonArray();
        foreach (var b in items.OfType<JsonObject>())
        {
            var copy = (JsonObject)b.DeepClone(); var group = String(copy["groupName"]) ?? "";
            copy.Remove("groupName"); Breakpoint(copy);
            if (groups[group] is not JsonArray list) groups[group] = list = new JsonArray(); list.Add(copy);
        }
        return groups;
    }
    private static JsonNode ParallelStacks(JsonObject obj)
    {
        if (obj["frames"] is not JsonArray frames || HasDiagnostics(obj)) return obj;
        var groups = new JsonArray();
        foreach (var grouping in frames.OfType<JsonObject>().GroupBy(f => f["threadId"]?.ToJsonString()))
        {
            var first = grouping.First(); var thread = new JsonObject { ["id"] = Copy(first["threadId"]) };
            if (first["threadName"] is { } name) thread["name"] = name.DeepClone();
            var stack = new JsonArray();
            foreach (var frame in grouping) { var copy = (JsonObject)frame.DeepClone(); Remove(copy, "threadId", "threadName"); CompactFrame(copy); stack.Add(copy); }
            thread["frames"] = stack; groups.Add(thread);
        }
        return new JsonObject { ["threads"] = groups };
    }
    private static JsonNode Modules(JsonObject obj, Dictionary<string, JsonElement> args)
    {
        if (obj["modules"] is not JsonArray modules) return obj;
        var offset = args.TryGetValue("offset", out var start) && start.TryGetInt32(out var startValue) ? startValue : 0;
        var maximum = args.TryGetValue("maxItems", out var count) && count.TryGetInt32(out var countValue) ? countValue : 100;
        var rows = modules.OfType<JsonObject>().ToArray();
        var ambiguousPids = rows.GroupBy(module => module["processId"]?.ToJsonString())
            .Where(group => group.Select(module => module["processUniqueId"]?.ToJsonString()).Distinct().Skip(1).Any())
            .Select(group => group.Key).ToHashSet();
        var processes = new JsonArray();
        foreach (var group in rows.Skip(offset).Take(maximum).GroupBy(m => (m["processId"]?.ToJsonString(), m["processUniqueId"]?.ToJsonString())))
        {
            var process = new JsonObject { ["processId"] = Copy(group.First()["processId"]) };
            var ambiguousPid = ambiguousPids.Contains(group.Key.Item1);
            if (process["processId"] is null || ambiguousPid) process["processUniqueId"] = Copy(group.First()["processUniqueId"]);
            var list = new JsonArray();
            foreach (var module in group)
            {
                var m = (JsonObject)module.DeepClone();
                var binary = String(m["path"]); var pdb = String(m["symbolFile"]);
                m["symbols"] = IsTrue(m["symbolsLoaded"])
                    ? JsonValue.Create(pdb is not null && binary is not null && string.Equals(Path.ChangeExtension(binary, ".pdb").Replace('\\', '/'), pdb.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase) ? ".pdb" : pdb ?? "loaded")
                    : Copy(m["symbolStatus"]) ?? JsonValue.Create("unavailable");
                Remove(m, "processId", "processUniqueId", "name", "moduleId", "size", "loadAddress", "version", "symbolsLoaded", "symbolFile", "symbolStatus");
                list.Add(m);
            }
            process["modules"] = list; processes.Add(process);
        }
        obj.Remove("modules");
        obj["processes"] = processes;
        if ((long)offset + maximum < rows.Length) obj["nextOffset"] = offset + maximum;
        return obj;
    }
    private static JsonNode ExceptionSettings(JsonObject obj)
    {
        if (obj["settings"] is not JsonArray settings) return obj;
        var map = new JsonObject();
        foreach (var setting in settings.OfType<JsonObject>())
        {
            var name = String(setting["name"]); if (name is null || map.ContainsKey(name)) return obj;
            var copy = (JsonObject)setting.DeepClone(); copy.Remove("name"); map[name] = copy;
        }
        return map;
    }
    private static JsonNode Events(JsonObject obj)
    {
        if (obj["events"] is JsonArray events) foreach (var e in events.OfType<JsonObject>())
        {
            foreach (var key in new[] { "sessionId", "epoch" }) if (JsonNode.DeepEquals(obj[key], e[key])) e.Remove(key);
            if (e["debugger"] is JsonObject debugger && JsonNode.DeepEquals(obj["sessionId"], debugger["sessionId"])) debugger.Remove("sessionId");
        }
        if (IsFalse(obj["requiresReconciliation"])) obj.Remove("requiresReconciliation"); return obj;
    }
    private static void Coordinates(JsonObject obj, string prefix)
    {
        if (String(obj[prefix + "File"]) is { } file)
        {
            obj[prefix + "Location"] = $"{file}:{obj[prefix + "Line"]}:{obj[prefix + "Column"]}";
            Remove(obj, prefix + "File", prefix + "Line", prefix + "Column");
        }
    }
    private static void Session(JsonObject obj)
    {
        if (String(obj["activeDocument"]) is { } active)
            obj["activeDocument"] = BrokerToolService.CompactSourcePath(active, String(obj["solutionPath"]));
        Rename(obj, "solutionPath", "solution"); Remove(obj, "solutionName", "capabilities", "lastSeenUtc", "visualStudioVersion", "edition", "vsixVersion");
    }
    private static JsonNode Context(JsonObject obj)
    {
        if (obj["session"] is JsonObject session) { obj["sessionId"] = Copy(session["sessionId"]); obj.Remove("session"); }
        if (obj["solution"] is JsonObject solution) obj["solution"] = Copy(solution["path"]);
        Rename(obj, "activeDocument", "document");
        Remove(obj, "build", "errors", "pendingEdits");
        if (obj["selection"] is JsonObject selection) obj["selection"] = Selection(selection);
        if (obj["debugger"] is JsonObject debugger && JsonNode.DeepEquals(debugger["sessionId"], obj["sessionId"])) debugger.Remove("sessionId");
        return obj;
    }
    private static JsonNode Doctor(JsonObject obj)
    {
        var result = Select(obj, "healthy");
        if (obj["checks"] is JsonArray checks)
        {
            var issues = new JsonArray(checks.OfType<JsonObject>().Where(c => !IsTrue(c["passed"]) || String(c["severity"]) is "Warning" or "Error").Select(c => (JsonNode)c.DeepClone()).ToArray());
            if (issues.Count > 0) result["checks"] = issues;
        }
        return result;
    }
    private static JsonNode Build(JsonObject obj, bool status)
    {
        if (obj["status"] is JsonObject nested) { foreach (var pair in nested) if (!obj.ContainsKey(pair.Key)) obj[pair.Key] = Copy(pair.Value); obj.Remove("status"); }
        var result = Select(obj, "buildId", "outcome", "message", "lastBuildInfo");
        if (status) { if (obj["state"] is { } state) result["state"] = state.DeepClone(); if (obj["lastBuildInfo"] is { } count) result["lastBuildInfo"] = count.DeepClone(); }
        // Older VSIX versions may not report an outcome; retain their observed status.
        if (result["outcome"] is null) Merge(result, Select(obj, "state", "lastBuildInfo"));
        return result;
    }
    private static JsonObject Diagnostics(JsonObject obj)
    {
        var result = (JsonObject)obj.DeepClone(); result.Remove("items");
        if (obj["items"] is not JsonArray items) return result;
        foreach (var item in items.OfType<JsonObject>())
        {
            var diagnostic = (JsonObject)item.DeepClone();
            var level = String(diagnostic["level"]) ?? String(diagnostic["severity"]) ?? "Message";
            var key = level.Equals("Error", StringComparison.OrdinalIgnoreCase) || level.Equals("vsBuildErrorLevelHigh", StringComparison.OrdinalIgnoreCase) ? "errors" :
                level.Equals("Warning", StringComparison.OrdinalIgnoreCase) || level.Equals("vsBuildErrorLevelMedium", StringComparison.OrdinalIgnoreCase) ? "warnings" : "messages";
            Remove(diagnostic, "level", "severity"); Rename(diagnostic, "location", "position"); Rename(diagnostic, "message", "description");
            if (result[key] is not JsonArray list) result[key] = list = new JsonArray(); list.Add(diagnostic);
        }
        return result;
    }
    private static JsonNode DocumentDiagnostics(JsonObject obj)
    {
        var path = String(obj["documentPath"]);
        Rename(obj, "documentPath", "file");
        Remove(obj, "source", "language", "offset", "totalItems", "versionMatches", "isCurrent", "projectId");
        if (IsTrue(obj["linkedFile"])) Rename(obj, "projectName", "project");
        Remove(obj, "linkedFile", "projectName");
        if (obj["items"] is JsonArray items)
            foreach (var item in items.OfType<JsonObject>())
            {
                if (String(item["location"]) is { } location && path is not null &&
                    location.StartsWith(path + ":", StringComparison.OrdinalIgnoreCase))
                {
                    item["start"] = location[(path.Length + 1)..];
                    item.Remove("location");
                }
                if (item["endLine"] is { } line && item["endColumn"] is { } column)
                    item["end"] = $"{line}:{column}";
                Remove(item, "endLine", "endColumn");
            }
        return Diagnostics(obj);
    }

    private static JsonNode Navigation(JsonObject obj)
    {
        var key = obj.ContainsKey("definitions") ? "definitions" : "implementations";
        if (obj[key] is not JsonArray locations) return obj;
        var result = new JsonArray();
        foreach (var location in locations.OfType<JsonObject>())
        {
            var entry = (JsonObject)location.DeepClone();
            if (entry["symbol"] is JsonObject symbol && obj["symbol"] is JsonObject parent && JsonNode.DeepEquals(symbol["name"], parent["name"])) entry.Remove("symbol");
            else if (entry["symbol"] is JsonObject related && related["name"] is { } name) entry["symbol"] = name.DeepClone();
            result.Add(entry.Count == 1 && entry["location"] is { } path ? path.DeepClone() : entry);
        }
        obj[key] = result;
        if (obj["symbol"] is JsonObject shared) obj["symbol"] = Copy(shared["name"]);
        Remove(obj, "position"); return obj;
    }
    private static JsonNode SymbolContext(JsonObject obj)
    {
        var result = Select(obj, "snippet");
        if (obj["definition"] is JsonObject definition) { var d = Navigation(definition) as JsonObject; result["symbol"] = Copy(d?["symbol"]); result["definitions"] = Copy(d?["definitions"]); }
        if (obj["references"] is JsonObject refs && refs["references"] is JsonArray references)
        {
            var compact = new JsonArray();
            const int maxReferences = 50;
            if (references.Count > maxReferences) result["referencesTruncated"] = true;
            foreach (var reference in references.OfType<JsonObject>().Take(maxReferences))
            {
                var copy = (JsonObject)reference.DeepClone();
                if (copy["symbol"] is JsonObject s && refs["symbol"] is JsonObject parent &&
                    JsonNode.DeepEquals(s["name"], parent["name"]) && JsonNode.DeepEquals(s["kind"], parent["kind"])) copy.Remove("symbol");
                compact.Add(copy.Count == 1 && copy["location"] is { } location ? location.DeepClone() : copy);
            }
            result["references"] = compact;
        }
        return result;
    }
    private static JsonNode Hierarchy(JsonObject obj)
    {
        if (obj["symbol"] is JsonObject symbol) obj["symbol"] = Copy(symbol["name"]);
        foreach (var key in new[] { "incoming", "outgoing", "children" }) if (obj[key] is JsonArray nodes)
            foreach (var child in nodes.OfType<JsonObject>())
            {
                Hierarchy(child); if (child["callSite"] is JsonObject site) { child["location"] = Copy(site["location"]); child.Remove("callSite"); }
                if (child["children"] is JsonArray children && children.Count == 0) child.Remove("children");
            }
        Remove(obj, "position"); return obj;
    }
    private static JsonNode Search(JsonObject obj, bool singleFile = false, Dictionary<string, JsonElement>? args = null)
    {
        if (obj["matches"] is JsonArray matches) foreach (var match in matches.OfType<JsonObject>()) { Rename(match, "location", "position"); Rename(match, "lineText", "text"); }
        if (singleFile && obj["matches"] is JsonArray { Count: > 0 } rows &&
            String(rows[0]?["position"]) is { } first)
        {
            var end = first.LastIndexOf(':');
            var start = end > 0 ? first.LastIndexOf(':', end - 1) : -1;
            if (start > 0)
            {
                var path = first[..start];
                obj["file"] = path;
                foreach (var row in rows.OfType<JsonObject>())
                {
                    if (String(row["position"]) is { } position && position.StartsWith(path + ":", StringComparison.OrdinalIgnoreCase))
                        row["position"] = position[(start + 1)..];
                    if (args?.TryGetValue("query", out var query) == true && query.ValueKind == JsonValueKind.String &&
                        String(row["matchText"]) == query.GetString()) row.Remove("matchText");
                }
            }
        }
        return Collection(obj, "matches", true, "query", "matchCount");
    }
    private static JsonNode Selection(JsonObject obj)
    {
        if (obj["document"] is JsonObject document)
        {
            obj["location"] = $"{String(document["path"])}:{obj["anchorLine"]}:{obj["anchorColumn"]}-{obj["activeLine"]}:{obj["activeColumn"]}";
            Remove(obj, "document", "anchorLine", "anchorColumn", "activeLine", "activeColumn");
        }
        return obj;
    }
    private static JsonNode TestDebug(JsonObject obj)
    {
        var outcome = String(obj["outcome"]);
        if (outcome == "accepted") return Select(obj, "outcome", "runId");
        if (outcome == "attached" || obj["testHostProcessId"] is not null && outcome is null)
            return new JsonObject { ["outcome"] = "attached", ["processId"] = Copy(obj["testHostProcessId"]) };
        var result = Select(obj, "runId", "outcome", "message", "backend"); var processes = new JsonObject();
        foreach (var role in new[] { "testHost", "testRunner" })
            if (obj[role + "ProcessId"] is { } id) processes[role] = new JsonObject { ["id"] = id.DeepClone(), ["name"] = Copy(obj[role + "ProcessName"]) };
        if (processes.Count > 0) result["processes"] = processes;
        // Preserve launch diagnostics if attachment did not succeed.
        if (String(obj["outcome"]) != "attached") foreach (var key in new[] { "commandLine", "workingDirectory", "targetPath", "attachTimeoutSeconds" }) if (obj[key] is { } v) result[key] = v.DeepClone();
        return result;
    }
    private static JsonNode TestRun(JsonObject obj)
    {
        if (obj["results"] is JsonArray { Count: 1 } single && single[0] is JsonObject test &&
            !obj.ContainsKey("nextOffset") && !IsTrue(obj["resultsTruncated"]))
            return Select(test, "name", "outcome", "duration", "message", "stackTrace", "evidenceTruncated");
        var result = Select(obj, "runId", "outcome", "backend", "message", "runnerExitCode", "coverage",
            "totalResults", "retainedResults", "nextOffset", "resultsTruncated");
        if (obj["results"] is JsonArray results && results.Count > 0) result["results"] = results.DeepClone();
        return result;
    }
    private static JsonNode TestComposite(JsonObject obj)
    {
        if (obj["results"] is not JsonObject results) return obj;
        if (obj["run"] is JsonObject original && original["runId"] is not null && results["runId"] is not null &&
            !JsonNode.DeepEquals(original["runId"], results["runId"])) return obj;
        var result = (JsonObject)results.DeepClone();
        if (obj["run"] is JsonObject run) foreach (var key in new[] { "runId", "outcome", "backend" }) if (!result.ContainsKey(key)) result[key] = Copy(run[key]);
        var summary = Select(result, "runId", "outcome", "runnerExitCode", "totalResults", "counts", "resultsTruncated");
        if (result["failureSample"] is JsonArray { Count: > 0 } failures) summary["failures"] = failures.DeepClone();
        if (String(result["outcome"]) is "runnerFailed" or "outcomeUnknown" or "incomplete")
            Merge(summary, Select(result, "message"));
        return summary;
    }
    private static JsonNode Coverage(JsonObject obj)
    {
        void Walk(JsonObject o)
        {
            Rename(o, "linesCovered", "covered"); Rename(o, "linesValid", "total");
            if (o["lines"] is JsonArray lines)
            {
                var map = new JsonObject(); foreach (var line in lines.OfType<JsonObject>()) if (line["number"] is { } number) map[number.ToJsonString()] = Copy(line["hits"]); o["lines"] = map;
            }
            foreach (var pair in o.ToArray()) if (pair.Value is JsonObject child) Walk(child); else if (pair.Value is JsonArray array) foreach (var item in array.OfType<JsonObject>()) Walk(item);
        }
        Walk(obj); return obj;
    }
}
