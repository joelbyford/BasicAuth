param(
    [string]$BaseUrl = "http://localhost:5057",
    [string]$User = "demoUser",
    [string]$Pass = "demoPass!123"
)

$authValue = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes("$User`:$Pass"))
$badAuthValue = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes("$User`:wrongPassword"))

Write-Host "\n1) Missing auth header -> expect 401"
curl.exe -i -X POST "$BaseUrl/bogus" -H "X-Forwarded-Proto: https"

Write-Host "\n2) Bad credentials -> expect 401"
curl.exe -i -X POST "$BaseUrl/bogus" -H "X-Forwarded-Proto: https" -H "Authorization: Basic $badAuthValue"

Write-Host "\n3) Valid credentials + bogus endpoint -> expect 404"
curl.exe -i -X POST "$BaseUrl/bogus" -H "X-Forwarded-Proto: https" -H "Authorization: Basic $authValue"

Write-Host "\n4) Valid credentials + valid endpoint -> expect 200"
curl.exe -i "$BaseUrl/health" -H "X-Forwarded-Proto: https" -H "Authorization: Basic $authValue"
