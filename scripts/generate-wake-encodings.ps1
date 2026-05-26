# generate-wake-encodings.ps1 — build wake-phrase-encodings.json for Sherpa KWS (ppinyin).
$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
Set-Location $repoRoot

$kwsDir = Join-Path $repoRoot 'models\sherpa-onnx-kws-zipformer-wenetspeech-3.3M-2024-01-01'
$tokens = Join-Path $kwsDir 'tokens.txt'
if (-not (Test-Path $tokens)) {
    throw "KWS model missing. Run: .\scripts\download-models.ps1 -IncludeKws"
}

$phraseFile = Join-Path $PSScriptRoot 'wake-phrases.txt'
if (-not (Test-Path $phraseFile)) {
    throw "Missing $phraseFile"
}

$phrases = Get-Content $phraseFile -Encoding UTF8 | Where-Object { $_.Trim().Length -gt 0 }
$utf8 = New-Object System.Text.UTF8Encoding $false
$raw = Join-Path $env:TEMP "amr-kw-raw-$([guid]::NewGuid().ToString('N')).txt"
$out = Join-Path $env:TEMP "amr-kw-out-$([guid]::NewGuid().ToString('N')).txt"
$lines = foreach ($p in $phrases) {
    $t = $p.Trim()
    $at = $t.Replace(' ', '_')
    "$t :2.5 #0.30 @${at}"
}
[System.IO.File]::WriteAllText($raw, ($lines -join "`n") + "`n", $utf8)

$env:PYTHONIOENCODING = 'utf-8'
if (-not (Get-Command sherpa-onnx-cli -ErrorAction SilentlyContinue)) {
    throw "sherpa-onnx-cli not found. pip install sherpa-onnx click sentencepiece pypinyin"
}

sherpa-onnx-cli text2token --tokens $tokens --tokens-type ppinyin $raw $out
if ($LASTEXITCODE -ne 0) { throw "text2token failed" }

$encoded = @(Get-Content $out -Encoding UTF8 | Where-Object { $_.Trim().Length -gt 0 })
if ($encoded.Count -lt $phrases.Count) {
    throw "Expected $($phrases.Count) encoded lines, got $($encoded.Count)"
}

$map = [ordered]@{}
for ($i = 0; $i -lt $phrases.Count; $i++) {
    $map[$phrases[$i].Trim()] = $encoded[$i].Trim()
}

$jsonPath = Join-Path $kwsDir 'wake-phrase-encodings.json'
[System.IO.File]::WriteAllText($jsonPath, ($map | ConvertTo-Json -Depth 3), $utf8)
Write-Host "OK $jsonPath" -ForegroundColor Green

Remove-Item $raw, $out -Force -ErrorAction SilentlyContinue
