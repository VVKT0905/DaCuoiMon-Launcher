Add-Type -AssemblyName System.IO.Compression.FileSystem
$z = [System.IO.Compression.ZipFile]::OpenRead((Join-Path $PSScriptRoot "ysm_test.jar"))

$classes = @(
    "com/elfmcys/yesstevemodel/oOo0O000oOOoOoo0O00OO000.class",
    "com/elfmcys/yesstevemodel/oO00Oo0ooOo0O0Oo0o0o0oo0.class",
    "com/elfmcys/yesstevemodel/O0OOOOOooO0000o0ooooo00o.class",
    "com/elfmcys/yesstevemodel/OO0o0OO0oo00OOoo0O0oOoo0.class"
)

foreach ($c in $classes) {
    $entry = $z.GetEntry($c)
    if ($entry) {
        $stream = $entry.Open()
        $ms = New-Object System.IO.MemoryStream
        $stream.CopyTo($ms)
        $bytes = $ms.ToArray()
        $stream.Dispose()
        $str = [System.Text.Encoding]::UTF8.GetString($bytes)
        Write-Host "=== $c ==="
        $matches = [System.Text.RegularExpressions.Regex]::Matches($str, '[\x20-\x7E]{5,}')
        foreach ($m in $matches) {
            if ($m.Value -match 'gui' -or $m.Value -match 'button' -or $m.Value -match 'select' -or $m.Value -match 'click' -or $m.Value -match 'model') {
                Write-Host "  $($m.Value)"
            }
        }
    }
}
$z.Dispose()
