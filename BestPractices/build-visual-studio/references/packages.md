# Packages And Dependencies

```json
package_restore({ "projectName": "NetVsMcp.Broker", "sessionId": "..." })
project_dependencies({ "projectName": "NetVsMcp.Broker", "sessionId": "..." })
nuget_list({ "projectName": "NetVsMcp.Broker", "sessionId": "..." })
nuget_search({ "query": "Newtonsoft.Json", "maxResults": 20, "includePrerelease": false, "sessionId": "..." })
nuget_install({ "projectName": "NetVsMcp.Broker", "packageId": "Polly", "version": "8.4.1", "sessionId": "..." })
nuget_update({ "projectName": "NetVsMcp.Broker", "packageId": "Polly", "version": "8.5.0", "sessionId": "..." })
nuget_uninstall({ "projectName": "NetVsMcp.Broker", "packageId": "Polly", "sessionId": "..." })
```

`project_dependencies` reads project XML directly. `nuget_search` queries nuget.org. Install/update/uninstall run `dotnet` under the hood and can be slow or mutating, so confirm unless explicitly requested.
