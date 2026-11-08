$port = 8080
$listener = New-Object System.Net.HttpListener
$listener.Prefixes.Add("http://localhost:$port/")

try {
    $listener.Start()
    Write-Host "[AIGRAV_VOICE_BRIDGE_READY]: Server running on http://localhost:$port/"
    
    while ($listener.IsListening) {
        $context = $listener.GetContext()
        $request = $context.Request
        $response = $context.Response
        
        $response.AddHeader("Access-Control-Allow-Origin", "*")
        $response.AddHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS")
        $response.AddHeader("Access-Control-Allow-Headers", "Content-Type")

        if ($request.HttpMethod -eq "OPTIONS") {
            $response.StatusCode = 200
            $response.Close()
            continue
        }

        $urlPath = $request.Url.AbsolutePath

        if ($urlPath -eq "/" -or $urlPath -eq "/index.html") {
            $filePath = "c:\AIGRAV\aigrav_voice_hud.html"
            if (Test-Path $filePath) {
                $bytes = [System.IO.File]::ReadAllBytes($filePath)
                $response.ContentType = "text/html; charset=utf-8"
                $response.ContentLength64 = $bytes.Length
                $response.OutputStream.Write($bytes, 0, $bytes.Length)
            } else {
                $response.StatusCode = 404
            }
        }
        elseif ($urlPath -eq "/api/voice-input" -and $request.HttpMethod -eq "POST") {
            $reader = New-Object System.IO.StreamReader($request.InputStream, [System.Text.Encoding]::UTF8)
            $body = $reader.ReadToEnd()
            Set-Content -Path "c:\AIGRAV\latest_prompt.json" -Value $body -Encoding UTF8
            
            try {
                $json = ConvertFrom-Json $body
                $promptText = $json.prompt
                Write-Host "[AIGRAV_VOICE_PROMPT]: $promptText"
                
                # Auto generate excel if requested in prompt
                if ($promptText -match "excel|breakdown|output excel") {
                    powershell -ExecutionPolicy Bypass -File "c:\AIGRAV\generate_excel.ps1"
                }
            } catch {
                Write-Host "[AIGRAV_RAW_PROMPT]: $body"
            }

            $respBytes = [System.Text.Encoding]::UTF8.GetBytes('{"status":"ok","received":true}')
            $response.ContentType = "application/json"
            $response.ContentLength64 = $respBytes.Length
            $response.OutputStream.Write($respBytes, 0, $respBytes.Length)
        }
        elseif ($urlPath -eq "/api/generate-excel") {
            powershell -ExecutionPolicy Bypass -File "c:\AIGRAV\generate_excel.ps1"
            $respBytes = [System.Text.Encoding]::UTF8.GetBytes('{"status":"excel_generated","file":"c:\\AIGRAV\\Breakdown_Volume_Beton_Rusun_Tower.csv"}')
            $response.ContentType = "application/json"
            $response.ContentLength64 = $respBytes.Length
            $response.OutputStream.Write($respBytes, 0, $respBytes.Length)
        }
        elseif ($urlPath -eq "/api/open-excel") {
            Start-Process "c:\AIGRAV\Breakdown_Volume_Beton_Rusun_Tower.csv"
            $respBytes = [System.Text.Encoding]::UTF8.GetBytes('{"status":"excel_opened"}')
            $response.ContentType = "application/json"
            $response.ContentLength64 = $respBytes.Length
            $response.OutputStream.Write($respBytes, 0, $respBytes.Length)
        }
        else {
            $response.StatusCode = 404
        }
        
        $response.Close()
    }
} catch {
    Write-Error $_.Exception.Message
} finally {
    if ($listener.IsListening) {
        $listener.Stop()
    }
}
