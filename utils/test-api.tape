# ══════════════════════════════════════════════════════════════════════════════
#  Tape VHS · ExpenseHub — Testes de Contrato da API
#  Como correr da raiz do projeto:  vhs utils/test-api.tape
# ══════════════════════════════════════════════════════════════════════════════

Output utils/expensehub-test.gif
Output utils/expensehub-test.mp4

Set Theme "Catppuccin Mocha"
Set FontFamily "Cascadia Code, JetBrains Mono, Consolas, monospace"
Set FontSize 27
Set LineHeight 1.2
Set LetterSpacing 0

Set Framerate 30
Set Width 1920
Set Height 2040

Set WindowBar Colorful
Set WindowBarSize 60
Set BorderRadius 18
Set Margin 42
Set MarginFill "#313244"
Set Padding 36

Set Shell pwsh
Set TypingSpeed 35ms
Set PlaybackSpeed 1.0
Set CursorBlink false

Hide
Sleep 1.5s
Type 'if (Test-Path "./utils/test-api.ps1") { Set-Location ./utils }; function prompt { "  `e[38;2;137;180;250m❯`e[0m " }; Clear-Host'
Enter
Sleep 800ms
Show

Sleep 200ms
Type "./test-api.ps1"
Sleep 200ms
Enter

# 5s de execução dos testes + 4.5s de pausa no resultado final
Sleep 9.5s