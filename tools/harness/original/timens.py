#!/usr/bin/env python3
"""timens.py SECONDS CMD... : run CMD in a new user + time namespace whose CLOCK_MONOTONIC and CLOCK_BOOTTIME read
SECONDS since boot (the original's park clock loses precision with uptime, TOOLING.md warning 1). util-linux
2.39 cannot parse a negative --monotonic offset, so the offsets are written to /proc/self/timens_offsets directly.
Ubuntu's kernel.apparmor_restrict_unprivileged_userns=1 refuses it (PermissionError on setgroups) unless it runs under a
profile allowed user namespaces: `aa-exec -p steam -- timens.py ...`, as original.sh starts the game (2026-10-20)."""
import ctypes, os, sys
want = float(sys.argv[1]); cmd = sys.argv[2:]
libc = ctypes.CDLL(None, use_errno=True)
CLONE_NEWUSER, CLONE_NEWTIME = 0x10000000, 0x00000080
uid, gid = os.getuid(), os.getgid()
up = float(open('/proc/uptime').read().split()[0])
off = -int(up - want)
if libc.unshare(CLONE_NEWUSER | CLONE_NEWTIME) != 0:
    e = ctypes.get_errno(); sys.exit('unshare: ' + os.strerror(e))
open('/proc/self/setgroups', 'w').write('deny')
open('/proc/self/uid_map', 'w').write(f'{uid} {uid} 1')
open('/proc/self/gid_map', 'w').write(f'{gid} {gid} 1')
open('/proc/self/timens_offsets', 'w').write(f'monotonic {off} 0\nboottime {off} 0\n')
pid = os.fork()
if pid == 0:
    os.execvp(cmd[0], cmd)
_, status = os.waitpid(pid, 0)
sys.exit(os.waitstatus_to_exitcode(status))
