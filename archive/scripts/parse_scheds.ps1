$lines = Get-Content 'C:\AIGRAV\all_schedules.txt'
for ($i=0; $i -lt $lines.Length; $i++) {
    if ($lines[$i] -match '\[Schedule: ''(.*?)''\]') {
        $name = $matches[1]
        $cat = $lines[$i+1]
        if ($cat -match 'Structural Columns|Structural Framing') {
            Write-Host "$cat | $name"
        }
    }
}
