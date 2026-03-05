param(
    [string]$BaseUrl = "http://localhost:5057",
    [string]$User = "demoUser",
    [string]$Pass = "demoPass!123"
)

$authValue = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes("$User`:$Pass"))
$badAuthValue = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes("$User`:wrongPassword"))

$allPassed = $true

function Assert-Status {
    param(
        [string]$Description,
        [string]$ExpectedStatus,
        [string[]]$CurlArgs
    )

    $actualStatus = & curl.exe -s -o NUL -w "%{http_code}" @CurlArgs

    if ($actualStatus -eq $ExpectedStatus) {
        Write-Host "PASS: $Description (expected $ExpectedStatus, got $actualStatus)"
    }
    else {
        Write-Host "FAIL: $Description (expected $ExpectedStatus, got $actualStatus)"
        $script:allPassed = $false
    }
}

Assert-Status -Description "Missing auth header" -ExpectedStatus "401" -CurlArgs @(
    "-X", "POST", "$BaseUrl/bogus",
    "-H", "X-Forwarded-Proto: https"
)

Assert-Status -Description "Bad credentials" -ExpectedStatus "401" -CurlArgs @(
    "-X", "POST", "$BaseUrl/bogus",
    "-H", "X-Forwarded-Proto: https",
    "-H", "Authorization: Basic $badAuthValue"
)

Assert-Status -Description "Valid credentials + bogus endpoint" -ExpectedStatus "404" -CurlArgs @(
    "-X", "POST", "$BaseUrl/bogus",
    "-H", "X-Forwarded-Proto: https",
    "-H", "Authorization: Basic $authValue"
)

Assert-Status -Description "Valid credentials + valid endpoint" -ExpectedStatus "200" -CurlArgs @(
    "$BaseUrl/health",
    "-H", "X-Forwarded-Proto: https",
    "-H", "Authorization: Basic $authValue"
)

if ($allPassed) {
    Write-Output "true"
    exit 0
}
else {
    Write-Output "false"
    exit 1
}
