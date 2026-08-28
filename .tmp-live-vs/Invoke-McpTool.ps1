param(
    [Parameter(Mandatory = $true)]
    [string]$Name,

    [hashtable]$Arguments = @{},

    [string]$Endpoint = "http://127.0.0.1:5050/mcp"
)

$script:NextId = 1

function Invoke-McpRequest {
    param(
        [string]$Method,
        [hashtable]$Params = @{}
    )

    $body = @{
        jsonrpc = "2.0"
        id = $script:NextId
        method = $Method
        params = $Params
    } | ConvertTo-Json -Depth 20
    $script:NextId++

    $response = Invoke-WebRequest -UseBasicParsing -Method Post -Uri $Endpoint -ContentType "application/json" -Headers @{
        Accept = "application/json, text/event-stream"
    } -Body $body
    $text = $response.Content
    $jsonStart = $text.IndexOf("{")
    if ($jsonStart -lt 0) {
        throw "No JSON object returned: $text"
    }

    return $text.Substring($jsonStart) | ConvertFrom-Json
}

Invoke-McpRequest -Method "initialize" -Params @{
    protocolVersion = "2025-11-25"
    capabilities = @{}
    clientInfo = @{
        name = "netvsmcp-live-test"
        version = "1.0"
    }
} | Out-Null

$result = Invoke-McpRequest -Method "tools/call" -Params @{
    name = $Name
    arguments = $Arguments
}

$result | ConvertTo-Json -Depth 50
