# DineMaster: lokal ishlab chiqish uchun maxfiy sozlamalarni (user-secrets) kiritish.
# Ishga tushirish (istalgan papkadan):
#   powershell -ExecutionPolicy Bypass -File C:\Users\user\DineMaster\scripts\setup-dev-secrets.ps1
#
# - PostgreSQL parolini so'raydi (ekranda ko'rinmaydi); allaqachon sozlangan bo'lsa — o'tkazib yuborish mumkin
# - JWT uchun tasodifiy kalit yaratadi (bor bo'lsa — tegmaydi)
# - Ixtiyoriy: email (SMTP) — Gmail, Brevo, Resend va boshqalar

$ErrorActionPreference = 'Stop'
$api = Join-Path $PSScriptRoot '..\backend\DineMaster.Api'

function Read-Plain([string]$prompt) {
    $secure = Read-Host -Prompt $prompt -AsSecureString
    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    try { [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr) }
}

function Set-Secret([string]$key, [string]$value) {
    dotnet user-secrets set $key $value --project $api | Out-Null
}

$existing = (dotnet user-secrets list --project $api) -join "`n"
$hasDb = $existing -match 'ConnectionStrings:DefaultConnection'
$hasJwt = $existing -match 'Jwt:Secret'

# ── Ma'lumotlar bazasi ──
Write-Host "== DineMaster: ma'lumotlar bazasi ==" -ForegroundColor Cyan
$doDb = $true
if ($hasDb) {
    $ans = Read-Host "Baza ulanishi allaqachon sozlangan. Qayta kiritilsinmi? [y/N]"
    $doDb = $ans -match '^[yYhH]'
}
if ($doDb) {
    $dbHost = Read-Host "PostgreSQL host [localhost]"; if (-not $dbHost) { $dbHost = 'localhost' }
    $dbUser = Read-Host "PostgreSQL foydalanuvchi [postgres]"; if (-not $dbUser) { $dbUser = 'postgres' }
    $dbPass = Read-Plain "PostgreSQL paroli"
    Set-Secret "ConnectionStrings:DefaultConnection" "Host=$dbHost;Port=5432;Database=dine_master;Username=$dbUser;Password=$dbPass"
    Write-Host "Baza ulanishi saqlandi." -ForegroundColor Green
}

if (-not $hasJwt) {
    $bytes = New-Object byte[] 48
    [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
    Set-Secret "Jwt:Secret" ([Convert]::ToBase64String($bytes))
    Write-Host "JWT kaliti yaratildi." -ForegroundColor Green
}

# ── Email (SMTP) ──
Write-Host ""
Write-Host "== Email (tasdiqlash kodlari) ==" -ForegroundColor Cyan
Write-Host "Misollar:"
Write-Host "  Gmail : host smtp.gmail.com,       port 587, login = gmail manzil,  parol = App password"
Write-Host "  Brevo : host smtp-relay.brevo.com, port 587, login = SMTP login,    parol = SMTP key"
Write-Host "  Resend: host smtp.resend.com,      port 587, login = resend,        parol = API key"
$smtp = Read-Host "Email (SMTP) sozlansinmi? [y/N]"
if ($smtp -match '^[yYhH]') {
    $smtpHost = Read-Host "SMTP host [smtp.gmail.com]"; if (-not $smtpHost) { $smtpHost = 'smtp.gmail.com' }
    $smtpPort = Read-Host "SMTP port [587]"; if (-not $smtpPort) { $smtpPort = '587' }
    $smtpUser = Read-Host "SMTP login"
    $smtpPass = Read-Plain "SMTP paroli / kaliti"
    $from = Read-Host "Kimdan manzil, masalan noreply@dinemaster.uz [$smtpUser]"; if (-not $from) { $from = $smtpUser }
    $fromName = Read-Host "Kimdan nomi [DineMaster]"; if (-not $fromName) { $fromName = 'DineMaster' }
    $replyTo = Read-Host "Javob manzili (Reply-To), masalan support@dinemaster.uz [bo'sh qoldirish mumkin]"

    Set-Secret "Email:Smtp:Host" $smtpHost
    Set-Secret "Email:Smtp:Port" $smtpPort
    Set-Secret "Email:Smtp:Username" $smtpUser
    Set-Secret "Email:Smtp:Password" $smtpPass
    Set-Secret "Email:FromAddress" $from
    Set-Secret "Email:FromName" $fromName
    if ($replyTo) { Set-Secret "Email:ReplyTo" $replyTo }
    Write-Host "SMTP saqlandi. Xatlar '$fromName <$from>' nomidan yuboriladi." -ForegroundColor Green
} else {
    Write-Host "SMTP o'zgartirilmadi. Sozlanmagan bo'lsa, Development'da kodlar backend konsoliga chiqadi." -ForegroundColor Yellow
}

# ── Telegram bot (ixtiyoriy) ──
Write-Host ""
Write-Host "== Telegram bildirishnomalari (ixtiyoriy) ==" -ForegroundColor Cyan
Write-Host "Telegram'da @BotFather -> /newbot -> bot nomi va username (masalan DineMasterBot) -> token beriladi."
$tg = Read-Host "Telegram bot sozlansinmi? [y/N]"
if ($tg -match '^[yYhH]') {
    $tgToken = Read-Plain "Bot token (123456:ABC...)"
    $tgUser = Read-Host "Bot username (@ belgisiz, masalan DineMasterBot)"
    Set-Secret "Telegram:BotToken" $tgToken
    Set-Secret "Telegram:BotUsername" ($tgUser.TrimStart('@'))
    Write-Host "Telegram saqlandi. Foydalanuvchilar Profil -> Sozlamalar -> Telegram -> 'Ulash' orqali ulanadi." -ForegroundColor Green
}

Write-Host ""
Write-Host "Tayyor. Backendni qayta ishga tushiring: cd C:\Users\user\DineMaster\backend\DineMaster.Api; dotnet run" -ForegroundColor Cyan
