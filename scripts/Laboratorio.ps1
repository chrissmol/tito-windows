# Laboratório do Tito: compila, roda todas as conferências, tira fotos de cada tela, tema e olhar e junta tudo em 2 imagens.
# Saída em Dados\: laboratorio.txt (resultado), laboratorio-telas.png, laboratorio-olhos.png.
param([switch]$SkipBuild)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$dados=Join-Path $root 'Dados'
[IO.Directory]::CreateDirectory($dados) | Out-Null
$was=@(Get-Process TitoGlass -ErrorAction SilentlyContinue).Count -gt 0
if($was){Get-Process TitoGlass | Stop-Process -Force; Start-Sleep 1}
$lines=@(); $fail=$false
function Step($name,[scriptblock]$run){
  try{$out=& $run; $script:lines+=('OK    '+$name+' - '+(($out|Out-String).Trim() -replace '\s+',' '))}
  catch{$script:fail=$true; $script:lines+=('FALHOU '+$name+' - '+$_.Exception.Message)}
}
if(-not $SkipBuild){Step 'Compilar' {& (Join-Path $root 'Compilar.ps1')}}
if(-not $fail){
  Step 'Telas, lembretes, notas, foco, temas, olhos' {& (Join-Path $PSScriptRoot 'Test.ps1')}
  Step 'Abrir, fechar, esconder, voltar' {& (Join-Path $PSScriptRoot 'Test-Lifecycle.ps1')}
}
Add-Type -AssemblyName System.Drawing
function Sheet($files,$cols,$w,$h,$out){
  if(@($files).Count -eq 0){return}
  $rows=[Math]::Ceiling(@($files).Count/$cols); $label=16
  $bmp=New-Object Drawing.Bitmap ($cols*$w),($rows*($h+$label)); $g=[Drawing.Graphics]::FromImage($bmp)
  $g.Clear([Drawing.Color]::FromArgb(32,36,46)); $g.InterpolationMode='HighQualityBicubic'
  $font=New-Object Drawing.Font 'Segoe UI',8; $i=0
  foreach($f in $files){
    $x=($i%$cols)*$w; $y=[Math]::Floor($i/$cols)*($h+$label)
    $img=[Drawing.Image]::FromFile($f.FullName); $g.DrawImage($img,$x,$y+$label,$w,$h); $img.Dispose()
    $g.DrawString(($f.BaseName -replace '^preview-',''),$font,[Drawing.Brushes]::White,$x+2,$y+1); $i++
  }
  $bmp.Save($out); $g.Dispose(); $bmp.Dispose()
}
$all=Get-ChildItem $dados -Filter 'preview-*.png' -ErrorAction SilentlyContinue | Sort-Object Name
Sheet ($all | Where-Object {$_.Name -notmatch '^preview-(mascot|rest)'}) 4 300 135 (Join-Path $dados 'laboratorio-telas.png')
Sheet ($all | Where-Object {$_.Name -match '^preview-(mascot|rest)'}) 4 256 68 (Join-Path $dados 'laboratorio-olhos.png')
$head=if($fail){'LABORATORIO: FALHOU'}else{'LABORATORIO: TUDO OK'}
$report=($head,(Get-Date -Format 'yyyy-MM-dd HH:mm')) -join ' · '
$text=$report+"`r`n"+($lines -join "`r`n")+"`r`nFotos: Dados\laboratorio-telas.png (telas e temas) e Dados\laboratorio-olhos.png (olhares)"
[IO.File]::WriteAllText((Join-Path $dados 'laboratorio.txt'),$text)
Write-Output $text
if($env:GITHUB_STEP_SUMMARY){Add-Content $env:GITHUB_STEP_SUMMARY ("## "+$report+"`n``````"+"`n"+($lines -join "`n")+"`n``````")}
elseif($was -and (Test-Path (Join-Path $root 'Tito.exe'))){Start-Process (Join-Path $root 'Tito.exe')}
if($fail){exit 1}
