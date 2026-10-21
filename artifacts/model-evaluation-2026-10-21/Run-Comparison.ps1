param([switch]$ResumeAfterKnownPreProviderFailure)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Net.Http

$artifactRoot = $PSScriptRoot
$sampleRoot = Join-Path $artifactRoot 'samples'
$resultPath = Join-Path $artifactRoot 'results.json'
$models = [ordered]@{
    'gpt-5.6-luna' = 'http://127.0.0.1:5178'
    'gpt-6-luna' = 'http://127.0.0.1:5179'
}
$sessions = @{}
$results = [System.Collections.Generic.List[object]]::new()
$attemptCount = 0
$failureMessage = $null
$completedKeys = @{}

if (Test-Path -LiteralPath $resultPath) {
    if (-not $ResumeAfterKnownPreProviderFailure) {
        throw 'Partial results exist. Resume only when the previous attempt is confirmed to have failed before reaching OpenAI.'
    }

    $existingResults = @(Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json)
    foreach ($existingResult in $existingResults) {
        $key = "$($existingResult.ConfiguredModel)|$($existingResult.Sample)"
        if ($completedKeys.ContainsKey($key) -or $existingResult.HttpStatus -ne 201) {
            throw 'Existing evaluation results are invalid or duplicated; refusing to continue.'
        }

        $completedKeys[$key] = $true
        $results.Add($existingResult)
    }

    $expectedPriorResults = @(
        'gpt-5.6-luna|01_invoice_digital.pdf',
        'gpt-6-luna|01_invoice_digital.pdf'
    )
    if ($existingResults.Count -ne $expectedPriorResults.Count -or
        @($completedKeys.Keys | Where-Object { $_ -notin $expectedPriorResults }).Count -gt 0) {
        throw 'Existing evaluation results do not match the known completed requests; refusing to continue.'
    }

    $attemptCount = 3
}
elseif ($ResumeAfterKnownPreProviderFailure) {
    throw 'Cannot resume because no partial evaluation results exist.'
}

function New-ModelSession([string]$BaseUrl) {
    $handler = [System.Net.Http.HttpClientHandler]::new()
    $handler.CookieContainer = [System.Net.CookieContainer]::new()
    $client = [System.Net.Http.HttpClient]::new($handler)
    $client.Timeout = [TimeSpan]::FromMinutes(3)

    try {
        $tokenResponse = $client.GetAsync("$BaseUrl/api/v1/documents/antiforgery").GetAwaiter().GetResult()
        $tokenResponse.EnsureSuccessStatusCode()
        $tokenJson = $tokenResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json
    }
    finally {
        if ($tokenResponse) { $tokenResponse.Dispose() }
    }

    return [pscustomobject]@{ Handler = $handler; Client = $client; Token = $tokenJson.requestToken }
}

function Send-SyntheticDocument(
    [string]$Model,
    [string]$BaseUrl,
    [object]$Session,
    [System.IO.FileInfo]$File) {
    $mediaType = switch ($File.Extension.ToLowerInvariant()) {
        '.pdf' { 'application/pdf' }
        '.png' { 'image/png' }
        '.jpg' { 'image/jpeg' }
        default { throw "Unsupported synthetic format: $($File.Extension)" }
    }

    $content = [System.Net.Http.MultipartFormDataContent]::new()
    $stream = [System.IO.File]::OpenRead($File.FullName)
    $fileContent = [System.Net.Http.StreamContent]::new($stream)
    $fileContent.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse($mediaType)
    $content.Add($fileContent, 'file', $File.Name)

    $request = [System.Net.Http.HttpRequestMessage]::new(
        [System.Net.Http.HttpMethod]::Post,
        "$BaseUrl/api/v1/documents/process")
    $request.Headers.Add('X-CSRF-TOKEN', $Session.Token)
    $request.Content = $content

    $script:attemptCount++
    $response = $null
    try {
        $response = $Session.Client.SendAsync($request).GetAwaiter().GetResult()
        $responseContent = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        $statusCode = [int]$response.StatusCode
        if ($statusCode -ne 201) {
            $failure = $null
            try { $failure = $responseContent | ConvertFrom-Json } catch { }
            throw "Attempt $script:attemptCount ($Model, $($File.Name)) returned HTTP $statusCode; code=$($failure.code). Evaluation stopped without retry."
        }

        $payload = $responseContent | ConvertFrom-Json
        $analysis = $payload.analysis
        $metadata = $payload.processingMetadata
        return [pscustomobject]@{
            Sample = $File.Name
            InputSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $File.FullName).Hash.ToLowerInvariant()
            ConfiguredModel = $Model
            ReturnedModel = $metadata.model
            HttpStatus = $statusCode
            DocumentId = $payload.id
            DocumentStatus = $payload.status
            RequiresReview = $analysis.requiresReview
            IssueCodes = @($analysis.issues | ForEach-Object { $_.code })
            Analysis = $analysis
            LatencyMs = $metadata.latencyMs
            InputTokens = $metadata.inputTokens
            CachedInputTokens = $metadata.cachedInputTokens
            CacheWriteTokens = $metadata.cacheWriteTokens
            OutputTokens = $metadata.outputTokens
            EstimatedCostUsd = $metadata.estimatedCostUsd
        }
    }
    finally {
        if ($response) { $response.Dispose() }
        $request.Dispose()
        $content.Dispose()
        $stream.Dispose()
    }
}

try {
    foreach ($model in $models.Keys) {
        $sessions[$model] = New-ModelSession $models[$model]
        Write-Output "Prepared antiforgery session for $model."
    }

    $sampleOrder = @(
        @{ Name = '01_invoice_digital.pdf'; FirstModel = 'gpt-5.6-luna' },
        @{ Name = '02_invoice_scan.png'; FirstModel = 'gpt-6-luna' },
        @{ Name = '03_invoice_multiline.pdf'; FirstModel = 'gpt-5.6-luna' },
        @{ Name = '04_invoice_inconsistent.jpg'; FirstModel = 'gpt-6-luna' },
        @{ Name = '05_purchase_order_unknown.pdf'; FirstModel = 'gpt-5.6-luna' }
    )

    foreach ($sample in $sampleOrder) {
        $secondModel = if ($sample.FirstModel -eq 'gpt-5.6-luna') { 'gpt-6-luna' } else { 'gpt-5.6-luna' }
        foreach ($model in @($sample.FirstModel, $secondModel)) {
            $key = "$model|$($sample.Name)"
            if ($completedKeys.ContainsKey($key)) { continue }

            $file = Get-Item -LiteralPath (Join-Path $sampleRoot $sample.Name)
            $record = Send-SyntheticDocument $model $models[$model] $sessions[$model] $file
            $results.Add($record)
            $completedKeys[$key] = $true
            Write-Output ('Upload attempt {0} · completed extraction {1}/10: {2} · {3} · {4} ms · estimated ${5}' -f $attemptCount, $results.Count, $model, $sample.Name, $record.LatencyMs, $record.EstimatedCostUsd)
        }
    }
}
catch {
    $failureMessage = $_.Exception.Message
}
finally {
    foreach ($session in $sessions.Values) {
        if ($session) { $session.Client.Dispose(); $session.Handler.Dispose() }
    }

    $json = $results | ConvertTo-Json -Depth 40
    [System.IO.File]::WriteAllText($resultPath, $json, [System.Text.UTF8Encoding]::new($false))
}

if ($failureMessage) {
    Write-Error $failureMessage
    exit 1
}

if ($results.Count -ne 10) {
    Write-Error "Expected exactly 10 successful provider responses; upload attempts=$attemptCount successes=$($results.Count)."
    exit 1
}

foreach ($group in ($results | Group-Object ConfiguredModel)) {
    $latencies = @($group.Group | ForEach-Object { [double]$_.LatencyMs } | Sort-Object)
    $medianLatency = $latencies[[int][Math]::Floor($latencies.Count / 2)]
    $totalCost = ($group.Group | Measure-Object -Property EstimatedCostUsd -Sum).Sum
    $inputTokens = ($group.Group | Measure-Object -Property InputTokens -Sum).Sum
    $outputTokens = ($group.Group | Measure-Object -Property OutputTokens -Sum).Sum
    Write-Output ('Summary: {0} · requests={1} · median latency={2} ms · input tokens={3} · output tokens={4} · estimated total=${5}' -f $group.Name, $group.Count, $medianLatency, $inputTokens, $outputTokens, $totalCost)
}

Write-Output "Saved sanitized synthetic evaluation results to $resultPath"
