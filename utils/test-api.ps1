#Requires -Version 7
# ──────────────────────────────────────────────────────────────────────────────
#  ExpenseHub · Testes de Contrato da API (:5245)
#  Pré-requisito: dotnet run --project sources/ExpenseHub.Api
#  Senha Admin:   $env:Seed__AdminPassword  ou  user-secrets (Seed:AdminPassword) da API
#  Uso:           . .\utils\test-api.ps1   (ou: pwsh -NoProfile -File utils\test-api.ps1)
#  Gravação:      vhs utils/test-api.tape  →  utils/expensehub-test.gif / .mp4
# ──────────────────────────────────────────────────────────────────────────────

$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$base       = "http://localhost:5245"
$adminEmail = "admin@expensehub.local"
$run        = Get-Date -Format "HHmmss"

# Senha do Admin: mesma origem da API — variável de ambiente Seed__AdminPassword
# ou user-secrets do projeto da API (Seed:AdminPassword). NUNCA em código.
function Get-AdminPassword {
    $red   = $PSStyle.Foreground.FromRgb(243, 139, 168)
    $reset = $PSStyle.Reset

    if ($env:Seed__AdminPassword) { return $env:Seed__AdminPassword }

    $apiProject = Join-Path (Split-Path $PSScriptRoot -Parent) "sources/ExpenseHub.Api/ExpenseHub.Api.csproj"
    $secretLine = dotnet user-secrets list --project $apiProject 2>$null |
                  Select-String -SimpleMatch "Seed:AdminPassword" |
                  Select-Object -First 1
    if ($secretLine -and $secretLine.Line -match "=\s*(.+)$") { return $Matches[1].Trim() }

    Write-Host ""
    Write-Host "  ${red}✗ ERRO${reset}  Senha do Admin não configurada para a suíte de contrato."
    Write-Host "           ╰─▸ defina a variável de ambiente Seed__AdminPassword ou execute:"
    Write-Host "              dotnet user-secrets set `"Seed:AdminPassword`" `"<senha-forte>`" --project sources/ExpenseHub.Api"
    throw "Seed:AdminPassword ausente — consulte a seção 'Configuração segura' do README."
}

$adminPass = Get-AdminPassword

$script:passCount = 0
$script:failCount = 0
$script:stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

# ── Paleta Catppuccin Mocha (ANSI 24-bit via $PSStyle) ───────────────────────
$S = $PSStyle
$C = @{
    Reset   = $S.Reset
    Bold    = $S.Bold
    Dim     = $S.Foreground.FromRgb(108, 112, 134)   # #6c7086 (Overlay0 / Muted)
    Text    = $S.Foreground.FromRgb(205, 214, 244)   # #cdd6f4 (Text principal)
    Cyan    = $S.Foreground.FromRgb(137, 220, 235)   # #89dceb (Sky / Destaques)
    Blue    = $S.Foreground.FromRgb(137, 180, 250)   # #89b4fa (Blue / Bordas e Títulos)
    Purple  = $S.Foreground.FromRgb(203, 166, 247)   # #cba6f7 (Mauve / Auth e Badges)
    Green   = $S.Foreground.FromRgb(166, 227, 161)   # #a6e3a1 (Green / Sucesso)
    Yellow  = $S.Foreground.FromRgb(249, 226, 175)   # #f9e2af (Yellow / Avisos e PUT)
    Red     = $S.Foreground.FromRgb(243, 139, 168)   # #f38ba8 (Red / Erros e DELETE)
    BgPass  = $S.Background.FromRgb(30, 48, 42)    + $S.Foreground.FromRgb(166, 227, 161) + $S.Bold
    BgFail  = $S.Background.FromRgb(54, 30, 42)    + $S.Foreground.FromRgb(243, 139, 168) + $S.Bold
    BgAuth  = $S.Background.FromRgb(44, 34, 58)    + $S.Foreground.FromRgb(203, 166, 247) + $S.Bold
    BgHead  = $S.Background.FromRgb(49, 50, 68)    + $S.Foreground.FromRgb(137, 220, 235) + $S.Bold # Surface0 + Sky
}

function Write-Banner {
    $w = 78
    Write-Host ""
    Write-Host ("  $($C.Blue)╭" + ("─" * $w) + "╮$($C.Reset)")
    
    $line1Plain = "  ◆ EXPENSEHUB  ·  Suíte de Testes de Contrato da API"
    $line2Plain = "  target: $base  ·  run: #$run"
    
    $pad1 = " " * [Math]::Max(0, ($w - $line1Plain.Length))
    $pad2 = " " * [Math]::Max(0, ($w - $line2Plain.Length))

    Write-Host "  $($C.Blue)│$($C.Reset)  $($C.Bold)$($C.Cyan)◆ EXPENSEHUB$($C.Reset)  $($C.Dim)·$($C.Reset)  $($C.Text)Suíte de Testes de Contrato da API$($C.Reset)$pad1$($C.Blue)│$($C.Reset)"
    Write-Host "  $($C.Blue)│$($C.Reset)  $($C.Dim)target: $base  ·  run: #$run$($C.Reset)$pad2$($C.Blue)│$($C.Reset)"
    Write-Host ("  $($C.Blue)╰" + ("─" * $w) + "╯$($C.Reset)")
}

function Write-Section {
    param([Parameter(Mandatory)][string]$Title)
    $parts = $Title -split "·", 2
    $num   = $parts[0].Trim()
    $name  = $parts[1].Trim()
    
    $lineLen = [Math]::Max(4, 73 - $name.Length)
    $line    = "─" * $lineLen
    Write-Host ""
    Write-Host "  $($C.BgHead) $num $($C.Reset) $($C.Bold)$($C.Blue)$name$($C.Reset) $($C.Dim)$line$($C.Reset)"
    Start-Sleep -Milliseconds 120
}

function Get-MethodBadge {
    param([string]$Method)
    $m = $Method.ToUpper().PadRight(4)
    $badge = switch ($Method.ToUpper()) {
        "GET"    { "$($C.Cyan)$m$($C.Reset)" }
        "POST"   { "$($C.Green)$m$($C.Reset)" }
        "PUT"    { "$($C.Yellow)$m$($C.Reset)" }
        "DELETE" { "$($C.Red)$m$($C.Reset)" }
        default  { "$($C.Dim)$m$($C.Reset)" }
    }
    return $badge
}

function Get-ShortId {
    param($Id)
    if (-not $Id) { return "" }
    $text = "$Id"
    return $text.Substring(0, [Math]::Min(8, $text.Length))
}

function Get-Summary {
    param($Json)
    if (-not $Json) { return "" }
    $parts = @()
    if ($Json -is [array]) {
        $parts += "items=$($Json.Count)"
        if ($Json.Count -eq 0) { return ($parts -join " ") }
        $Json = $Json[0]
    }
    if ($Json.id)     { $parts += "id=$(Get-ShortId $Json.id)" }
    if ($Json.state)  { $parts += "state=$($Json.state)" }
    if ($Json.amount) { $parts += ("R`$ {0:N2}" -f [decimal]$Json.amount) }
    if ($Json.roles)  { $parts += "roles=[" + ($Json.roles -join ",") + "]" }
    if ($Json.email)  { $parts += "$($Json.email)" }
    return ($parts -join " · ")
}

function Get-ProblemDetail {
    param($Json)
    if (-not $Json) { return "" }
    $message = $Json.detail
    if (-not $message -and $Json.errors) {
        $message = ($Json.errors.PSObject.Properties | ForEach-Object { $_.Value -join " / " }) -join " · "
    }
    if (-not $message) { return "" }
    return $message.Substring(0, [Math]::Min(90, $message.Length))
}

function Test-Route {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$Name,
        [string]$Method = "GET",
        [Parameter(Mandatory)][string]$Uri,
        [hashtable]$Headers = @{},
        [string]$Body,
        [int]$Expect = 0
    )

    $reqTimer = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $params = @{
            Uri                = $Uri
            Method             = $Method
            Headers            = $Headers
            ErrorAction        = "Stop"
            SkipHttpErrorCheck = $true
            TimeoutSec         = 60
        }
        if (-not [string]::IsNullOrEmpty($Body)) {
            $params.Body        = $Body
            $params.ContentType = "application/json"
        }
        $response = Invoke-WebRequest @params
        $reqTimer.Stop()
    } catch {
        $reqTimer.Stop()
        $script:failCount++
        Write-Host "  $($C.BgFail) ✗ FAIL $($C.Reset) $($C.Red)$Name · Erro de transporte: $($_.Exception.Message)$($C.Reset)"
        return $null
    }

    $ms   = $reqTimer.ElapsedMilliseconds
    $code = [int]$response.StatusCode
    $raw  = if ($response.Content -is [byte[]]) { [System.Text.Encoding]::UTF8.GetString($response.Content) } else { [string]$response.Content }
    $json = $null
    try { if ($raw) { $json = $raw | ConvertFrom-Json } } catch { }

    $passed    = if ($Expect -gt 0) { $code -eq $Expect } else { $code -lt 400 }
    $mBadge    = Get-MethodBadge $Method
    $codeColor = if ($code -lt 300) { $C.Green } elseif ($code -lt 500) { $C.Yellow } else { $C.Red }
    $namePad   = $Name.PadRight(32)
    $timeStr   = ("{0,4}ms" -f $ms)

    if ($passed) {
        $script:passCount++
        $summary = Get-Summary $json
        $sumStr  = if ($summary) { " $($C.Dim)│ $summary$($C.Reset)" } else { "" }
        Write-Host "  $($C.BgPass) ✓ PASS $($C.Reset) $codeColor$("{0,3}" -f $code)$($C.Reset)  $mBadge  $($C.Text)$namePad$($C.Reset) $($C.Dim)$timeStr$($C.Reset)$sumStr"
    } else {
        $script:failCount++
        Write-Host "  $($C.BgFail) ✗ FAIL $($C.Reset) $($C.Red)$("{0,3}" -f $code)$($C.Reset)  $mBadge  $($C.Text)$namePad$($C.Reset) $($C.Red)(esperado $Expect)$($C.Reset)"
        $detail = Get-ProblemDetail $json
        if ($detail) {
            Write-Host "           $($C.Red)╰─▸ $detail$($C.Reset)"
        }
    }

    return $json
}

function Get-Token {
    param($Email, $Password)
    try {
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        $token = (Invoke-RestMethod -Uri "$base/login" -Method Post -Body (@{ email = $Email; password = $Password } | ConvertTo-Json) -ContentType "application/json").accessToken
        $sw.Stop()
        $emailPad = $Email.PadRight(32)
        $timeStr  = ("{0,4}ms" -f $sw.ElapsedMilliseconds)
        Write-Host "  $($C.BgAuth) ◆ AUTH $($C.Reset) $($C.Purple)200$($C.Reset)  $($C.Green)POST$($C.Reset)  $($C.Text)$emailPad$($C.Reset) $($C.Dim)$timeStr │ JWT emitido$($C.Reset)"
        return $token
    } catch {
        Write-Host "  $($C.BgFail) ✗ AUTH $($C.Reset) $($C.Red)Falha no login para $Email$($C.Reset)"
        throw
    }
}

function New-Auth { param($Token) @{ Authorization = "Bearer $Token" } }
function Assert-Found { param($Value, [string]$Message) if (-not $Value) { throw $Message } }

# ══════════════════════════════════════════════════════════════════════════════
Write-Banner

$empEmail = "joao-$run@teste.com"
$aprEmail = "maria-$run@teste.com"
$finEmail = "ana-$run@teste.com"

# ── 01 · Health e 401 anônimo ─────────────────────────────────────────────────
Write-Section "01 · Health & Proteção Anônima (401)"
Test-Route -Name "Health check (anônimo)"       -Uri "$base/health"          -Expect 200 | Out-Null
Test-Route -Name "GET /api/expenses s/ token"    -Uri "$base/api/expenses"    -Expect 401 | Out-Null
Test-Route -Name "GET /api/admin/users s/ token" -Uri "$base/api/admin/users" -Expect 401 | Out-Null

# ── 02 · Login do Admin ───────────────────────────────────────────────────────
Write-Section "02 · Autenticação do Admin"
$adminHeaders = New-Auth (Get-Token $adminEmail $adminPass)
Test-Route -Name "Login com senha inválida" -Method Post -Uri "$base/login" -Body (@{ email = $adminEmail; password = "errada" } | ConvertTo-Json) -Expect 401 | Out-Null

# ── 03 · Cadastro público (sem role) ──────────────────────────────────────────
Write-Section "03 · Registro Público & Validações"
Test-Route -Name "Cadastro João (Employee)" -Method Post -Uri "$base/register" -Body (@{ email = $empEmail; password = "Forte@123" } | ConvertTo-Json) -Expect 201 | Out-Null
Test-Route -Name "Cadastro Maria (Approver)" -Method Post -Uri "$base/register" -Body (@{ email = $aprEmail; password = "Forte@123" } | ConvertTo-Json) -Expect 201 | Out-Null
Test-Route -Name "Cadastro Ana (Finance)"   -Method Post -Uri "$base/register" -Body (@{ email = $finEmail; password = "Forte@123" } | ConvertTo-Json) -Expect 201 | Out-Null
Test-Route -Name "Bloqueio de e-mail duplicado" -Method Post -Uri "$base/register" -Body (@{ email = $empEmail; password = "Forte@123" } | ConvertTo-Json) -Expect 400 | Out-Null
Test-Route -Name "Bloqueio de e-mail inválido"  -Method Post -Uri "$base/register" -Body (@{ email = "invalido"; password = "Forte@123" } | ConvertTo-Json) -Expect 400 | Out-Null
Test-Route -Name "Bloqueio de senha fraca"      -Method Post -Uri "$base/register" -Body (@{ email = "curta-$run@teste.com"; password = "F@1" } | ConvertTo-Json) -Expect 400 | Out-Null

# ── 04 · Gestão de roles (Admin) ──────────────────────────────────────────────
Write-Section "04 · Controle de Acesso & Roles (Admin)"
$users = Test-Route -Name "Listar usuários cadastrados" -Uri "$base/api/admin/users" -Headers $adminHeaders -Expect 200
Assert-Found $users "Listagem de usuários falhou — abortando."
$empId = ($users | Where-Object email -eq $empEmail).id
$aprId = ($users | Where-Object email -eq $aprEmail).id
$finId = ($users | Where-Object email -eq $finEmail).id
$admId = ($users | Where-Object email -eq $adminEmail).id
Assert-Found $empId "IDs não encontrados — abortando."

Test-Route -Name "Promover João → Employee" -Method Put -Uri "$base/api/admin/users/$empId/roles" -Headers $adminHeaders -Body '{"roles":["Employee"]}' -Expect 200 | Out-Null
Test-Route -Name "Promover Maria → Approver" -Method Put -Uri "$base/api/admin/users/$aprId/roles" -Headers $adminHeaders -Body '{"roles":["Approver"]}' -Expect 200 | Out-Null
Test-Route -Name "Promover Ana → Finance"   -Method Put -Uri "$base/api/admin/users/$finId/roles" -Headers $adminHeaders -Body '{"roles":["Finance"]}'  -Expect 200 | Out-Null

Test-Route -Name "Rejeitar role inexistente"    -Method Put -Uri "$base/api/admin/users/$empId/roles"      -Headers $adminHeaders -Body '{"roles":["Boss"]}'     -Expect 400 | Out-Null
Test-Route -Name "Usuário não encontrado (404)" -Method Put -Uri "$base/api/admin/users/nao-existe/roles"  -Headers $adminHeaders -Body '{"roles":["Employee"]}' -Expect 404 | Out-Null
Test-Route -Name "Impedir auto-remoção Admin"   -Method Put -Uri "$base/api/admin/users/$admId/roles"      -Headers $adminHeaders -Body '{"roles":["Employee"]}' -Expect 403 | Out-Null
Test-Route -Name "Impedir não-Admin gerir role" -Method Put -Uri "$base/api/admin/users/$empId/roles"      -Headers (New-Auth (Get-Token $empEmail "Forte@123")) -Body '{"roles":["Employee"]}' -Expect 403 | Out-Null

Test-Route -Name "Criar conta descartável"      -Method Post -Uri "$base/register" -Body (@{ email = "descartavel-$run@teste.com"; password = "Forte@123" } | ConvertTo-Json) -Expect 201 | Out-Null
$descId = ((Invoke-RestMethod -Uri "$base/api/admin/users" -Headers $adminHeaders) | Where-Object email -eq "descartavel-$run@teste.com").id
Test-Route -Name "Limpar roles do usuário ([])" -Method Put -Uri "$base/api/admin/users/$descId/roles" -Headers $adminHeaders -Body '{"roles":[]}' -Expect 200 | Out-Null
Test-Route -Name "Re-promover João → Employee"  -Method Put -Uri "$base/api/admin/users/$empId/roles"  -Headers $adminHeaders -Body '{"roles":["Employee"]}' -Expect 200 | Out-Null

# ── 05 · Logins por perfil ────────────────────────────────────────────────────
Write-Section "05 · Emissão de Tokens por Perfil"
$empHeaders = New-Auth (Get-Token $empEmail "Forte@123")
$aprHeaders = New-Auth (Get-Token $aprEmail "Forte@123")
$finHeaders = New-Auth (Get-Token $finEmail "Forte@123")

# ── 06 · Fluxo create → edit → submit → approve → pay ─────────────────────────
Write-Section "06 · Ciclo Feliz (Draft → Submitted → Approved → Paid)"
$expBody = @{ description = "Passagem aerea para reuniao com cliente"; amount = 1250.75; expenseDate = "2026-09-10" } | ConvertTo-Json
$exp = Test-Route -Name "Criar despesa em rascunho" -Method Post -Uri "$base/api/expenses" -Headers $empHeaders -Body $expBody -Expect 201
$expId = $exp.id
Assert-Found $expId "Create falhou — abortando."

Test-Route -Name "Consultar detalhe (Dono)"     -Uri "$base/api/expenses/$expId"         -Headers $empHeaders -Expect 200 | Out-Null
Test-Route -Name "Consultar histórico inicial"  -Uri "$base/api/expenses/$expId/history" -Headers $empHeaders -Expect 200 | Out-Null
Test-Route -Name "Atualizar rascunho (PUT)"     -Method Put  -Uri "$base/api/expenses/$expId" -Headers $empHeaders -Body (@{ description = "Passagem aerea executiva cliente"; amount = 1500.00; expenseDate = "2026-09-10" } | ConvertTo-Json) -Expect 200 | Out-Null

Test-Route -Name "Submeter para aprovação"      -Method Post -Uri "$base/api/expenses/$expId/submit"  -Headers $empHeaders -Expect 200 | Out-Null
Test-Route -Name "Aprovar despesa (Approver)"   -Method Post -Uri "$base/api/expenses/$expId/approve" -Headers $aprHeaders -Expect 200 | Out-Null
Test-Route -Name "Pagar reembolso (Finance)"    -Method Post -Uri "$base/api/expenses/$expId/pay"     -Headers $finHeaders -Expect 200 | Out-Null

Test-Route -Name "Bloquear Admin no histórico"  -Uri "$base/api/expenses/$expId/history" -Headers $adminHeaders -Expect 403 | Out-Null
Test-Route -Name "Listar despesas (Finance)"    -Uri "$base/api/expenses"                -Headers $finHeaders   -Expect 200 | Out-Null

# ── 07 · Entrada e transições inválidas ───────────────────────────────────────
Write-Section "07 · Consistência de Domínio & Transições Inválidas"
Test-Route -Name "Rejeitar descrição curta"     -Method Post -Uri "$base/api/expenses" -Headers $empHeaders -Body (@{ description = "curto"; amount = 10; expenseDate = "2026-09-10" } | ConvertTo-Json) -Expect 400 | Out-Null
Test-Route -Name "Rejeitar valor zerado (R$ 0)" -Method Post -Uri "$base/api/expenses" -Headers $empHeaders -Body (@{ description = "Descricao valida de teste"; amount = 0; expenseDate = "2026-09-10" } | ConvertTo-Json) -Expect 400 | Out-Null
Test-Route -Name "Rejeitar data no futuro"      -Method Post -Uri "$base/api/expenses" -Headers $empHeaders -Body (@{ description = "Descricao valida de teste"; amount = 10; expenseDate = "2030-01-01" } | ConvertTo-Json) -Expect 400 | Out-Null

Test-Route -Name "Impedir re-submit em Paid"    -Method Post -Uri "$base/api/expenses/$expId/submit"  -Headers $empHeaders -Expect 409 | Out-Null
Test-Route -Name "Impedir re-approve em Paid"   -Method Post -Uri "$base/api/expenses/$expId/approve" -Headers $aprHeaders -Expect 409 | Out-Null
Test-Route -Name "Impedir re-pay em Paid"       -Method Post -Uri "$base/api/expenses/$expId/pay"     -Headers $finHeaders -Expect 409 | Out-Null
Test-Route -Name "Impedir edição fora de Draft" -Method Put  -Uri "$base/api/expenses/$expId"         -Headers $empHeaders -Body $expBody -Expect 409 | Out-Null

# ── 08 · Isolamento e papéis (403/404) ────────────────────────────────────────
Write-Section "08 · Isolamento entre Tenants & Segregação (RBAC)"
$exp2 = Test-Route -Name "Criar 2º rascunho (João)" -Method Post -Uri "$base/api/expenses" -Headers $empHeaders -Body (@{ description = "Diaria em sao paulo congresso"; amount = 300.00; expenseDate = "2026-09-12" } | ConvertTo-Json) -Expect 201
$expId2 = $exp2.id
Assert-Found $expId2 "Create do 2º rascunho falhou — abortando."

Test-Route -Name "Registrar 2º Employee"        -Method Post -Uri "$base/register" -Body (@{ email = "outro-$run@teste.com"; password = "Forte@123" } | ConvertTo-Json) -Expect 201 | Out-Null
$outroId = ((Invoke-RestMethod -Uri "$base/api/admin/users" -Headers $adminHeaders) | Where-Object email -eq "outro-$run@teste.com").id
Test-Route -Name "Promover 2º → Employee"       -Method Put  -Uri "$base/api/admin/users/$outroId/roles" -Headers $adminHeaders -Body '{"roles":["Employee"]}' -Expect 200 | Out-Null
$outroHeaders = New-Auth (Get-Token "outro-$run@teste.com" "Forte@123")

Test-Route -Name "Ocultar detalhe de terceiros" -Uri "$base/api/expenses/$expId2"         -Headers $outroHeaders -Expect 404 | Out-Null
Test-Route -Name "Ocultar hist. de terceiros"   -Uri "$base/api/expenses/$expId2/history" -Headers $outroHeaders -Expect 404 | Out-Null

Test-Route -Name "Bloquear listagem p/ Admin"   -Uri "$base/api/expenses" -Headers $adminHeaders -Expect 403 | Out-Null
Test-Route -Name "Bloquear aprovação Employee"  -Method Post -Uri "$base/api/expenses/$expId2/approve" -Headers $empHeaders -Expect 403 | Out-Null
Test-Route -Name "Bloquear pagamento Employee"  -Method Post -Uri "$base/api/expenses/$expId2/pay"     -Headers $empHeaders -Expect 403 | Out-Null
Test-Route -Name "Bloquear criação Approver"    -Method Post -Uri "$base/api/expenses"                 -Headers $aprHeaders -Body $expBody -Expect 403 | Out-Null
Test-Route -Name "Bloquear envio Approver"      -Method Post -Uri "$base/api/expenses/$expId2/submit"  -Headers $aprHeaders -Expect 403 | Out-Null

# ── 09 · Reprovação (reject) ──────────────────────────────────────────────────
Write-Section "09 · Fluxo de Reprovação (Reject)"
Test-Route -Name "Submeter 2º rascunho"         -Method Post -Uri "$base/api/expenses/$expId2/submit" -Headers $empHeaders -Expect 200 | Out-Null
Test-Route -Name "Exigir justificativa mínima"  -Method Post -Uri "$base/api/expenses/$expId2/reject" -Headers $aprHeaders -Body (@{ justification = "curta" } | ConvertTo-Json) -Expect 400 | Out-Null
Test-Route -Name "Reprovar despesa (Approver)"  -Method Post -Uri "$base/api/expenses/$expId2/reject" -Headers $aprHeaders -Body (@{ justification = "Comprovante ilegivel, reenviar com nota fiscal" } | ConvertTo-Json) -Expect 200 | Out-Null
Test-Route -Name "Impedir pagamento reprovado"  -Method Post -Uri "$base/api/expenses/$expId2/pay"    -Headers $finHeaders -Expect 409 | Out-Null

# ══════════════════════════════════════════════════════════════════════════════
$script:stopwatch.Stop()
$total   = $script:passCount + $script:failCount
$elapsed = "{0:F2}s" -f $script:stopwatch.Elapsed.TotalSeconds
$isOk    = $script:failCount -eq 0
$bColor  = if ($isOk) { $C.Green } else { $C.Red }
$status  = if ($isOk) { "✔ SUCESSO" } else { "✖ FALHA" }

$barWidth = 22
$passBars = if ($total -gt 0) { [int][Math]::Round(($script:passCount / $total) * $barWidth) } else { 0 }
$failBars = $barWidth - $passBars
$bar      = "$($C.Green)" + ("━" * $passBars) + "$($C.Red)" + ("━" * $failBars) + "$($C.Reset)"

$w = 78
$leftPlain  = "  $status  ·  $($script:passCount)/$total passaram  ·  $($script:failCount) falhas  ·  $elapsed"
$rightPlain = "$("━" * $barWidth)  "
$padCount   = [Math]::Max(1, $w - $leftPlain.Length - $rightPlain.Length)
$pad        = " " * $padCount

Write-Host ""
Write-Host ("  $bColor╭" + ("─" * $w) + "╮$($C.Reset)")
Write-Host ("  $bColor│$($C.Reset)  $($C.Bold)$bColor$status$($C.Reset)  $($C.Dim)·$($C.Reset)  $($C.Green)$($script:passCount)$($C.Dim)/$total passaram$($C.Reset)  $($C.Dim)·$($C.Reset)  $($C.Red)$($script:failCount) falhas$($C.Reset)  $($C.Dim)·$($C.Reset)  $($C.Cyan)$elapsed$($C.Reset)$pad$bar  $bColor│$($C.Reset)")
Write-Host ("  $bColor╰" + ("─" * $w) + "╯$($C.Reset)")
Write-Host ""