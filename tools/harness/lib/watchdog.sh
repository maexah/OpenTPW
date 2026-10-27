#!/bin/bash
# watchdog.sh - one read-only look at the machine's health, run every minute by the user timer tpw-watchdog.timer.
# Costs no tokens and changes nothing: it writes one line per new problem (and one when it clears) to
# <work>/watchdog/alerts.log, which the Sentinel waits on (watchdog/wait-alert.sh).
. "$(dirname "$0")/paths.sh"
W=$WORK/watchdog; mkdir -p $W; LOG=$W/alerts.log; ST=$W/state; touch $ST
now=$(date '+%F %T'); declare -A cur
avail=$(awk '/MemAvailable/{print int($2/1024)}' /proc/meminfo)
top=$(ps -eo rss,pid,comm --sort=-rss --no-headers | head -1 | awk '{printf "%s pid %s %.1f GB",$3,$2,$1/1048576}')
[ $avail -lt 4096 ] && cur[mem]="memory low: ${avail} MB available; largest $top"
big=$(ps -eo rss,pid,etime,args --sort=-rss --no-headers | awk '$1>8388608{printf "pid %s %.1f GB up %s %s; ",$2,$1/1048576,$3,substr($4,1,60)}' | head -c 400)
[ -n "$big" ] && cur[big]="process over 8 GB: $big"
tmp=$(df --output=pcent /tmp | tail -1 | tr -dc 0-9); [ $tmp -gt 70 ] && cur[tmp]="/tmp (RAM) ${tmp}% full"
home=$(df --output=avail -BG /home | tail -1 | tr -dc 0-9); [ $home -lt 30 ] && cur[disk]="/home only ${home} GB free"
# a zombie counts only past two minutes: systemd's own (sd-close) helpers are reaped within one
z=$(ps -eo stat,pid,ppid,etimes,comm --no-headers | awk '$1~/^Z/ && $4>120{printf "%s(parent %s) ",$5,$3}' | head -c 400)
[ -n "$z" ] && cur[zombie]="zombies: $z"
stale=$(ps -eo pid,etimes,args --no-headers | awk '$2>10800 && ($3~/(testhost|OpenTPW\.dll|OpenTPW\.Tests|Xvfb|gdb|TP-nodisc|wineserver)/ || $0~/dotnet (test|build)/) && !/awk/{printf "pid %s %dh %s; ",$1,$2/3600,substr($3,1,50)}' | head -c 400)
[ -n "$stale" ] && cur[stale]="leftover over 3 h: $stale"
hog=$(ps -eo pid,etimes,pcpu,args --no-headers | awk '$2>1800 && $3>80 && $0~/(dotnet|testhost|OpenTPW|python3|wine|Xvfb|gdb|objdump)/ && !/(VBCSCompiler|ghidra|claude)/{printf "pid %s %d%% %s; ",$1,$3,substr($4,1,50)}' | head -c 400)
[ -n "$hog" ] && cur[cpu]="busy for over 30 min: $hog"
for k in "${!cur[@]}"; do grep -qx "$k" $ST || echo "$now ALERT ${cur[$k]}" >> $LOG; done
while read -r k; do [ -n "$k" ] && [ -z "${cur[$k]+x}" ] && echo "$now CLEAR $k" >> $LOG; done < $ST
printf '%s\n' "${!cur[@]}" | grep -v '^$' > $ST.new; mv $ST.new $ST
