#!/usr/bin/env bash
LOG="${1:-$HOME/telia-test/latest/ping-1111.log}"
awk '
  /no answer yet/ {
    if (!in_gap) {
      ts = $1; gsub(/[\[\]]/, "", ts); gap_start = ts; in_gap = 1
    }
  }
  /bytes from/ {
    if (in_gap) {
      ts = $1; gsub(/[\[\]]/, "", ts)
      dur = ts - gap_start
      count++
      total += dur
      last = dur
      last_time = gap_start
      in_gap = 0
    }
  }
  END {
    cmd = "date -d @" last_time " \"+%Y-%m-%d %H:%M:%S\" 2>/dev/null"
    print "Loss events so far: " count+0
    if (count > 0) {
      cmd | getline human_time
      close(cmd)
      printf "Most recent gap: %.1fs, started ~%s\n", last, human_time
      printf "Total lost time so far: %.1fs\n", total
    }
    if (in_gap) print "*** CURRENTLY IN A GAP (ongoing right now) ***"
  }
' "$LOG"
