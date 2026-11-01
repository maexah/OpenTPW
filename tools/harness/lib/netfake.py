#!/usr/bin/env python3
"""netfake.py - a run's stand-ins for the network and the browser, so a game's calls out can be counted and none leaves the machine (Q941).

    import netfake
    srv, conns = netfake.listener( "record" )       # "record": accept, note the first line, close; "hole": accept and never answer; "closed": nothing listens
    netfake.proxy_env( srv )                          # HTTPS_PROXY / https_proxy of this process at it (the game reads them); clear_proxy() takes them off
    opened = netfake.browser_shim( OUT )              # an xdg-open that records its arguments in OUT/opened.txt, first on PATH, and no D-Bus portal;
                                                      # opened() answers what SDL_OpenURL handed it (nothing opens)

Set both BEFORE the game starts (the child takes this process's environment at launch: `parkrun.py` and `gamedrive.py` read os.environ then).
`conns` is a list of (time, first line the client sent), e.g. 'CONNECT api.github.com:443 HTTP/1.1': it counts CONNECTIONS, and .NET opens
more than one when a proxy hangs up on it at once, so one request can be sixteen entries (Q941); a "hole" is held open, so it counts one.
`python3 netfake.py` is its own check: a connection and a fake browser call, both seen."""
import os, socket, subprocess, sys, tempfile, threading, time


def listener(mode="record"):
    """A loopback server standing in for a proxy. Answers (the server socket, the list its connections are noted in).
    mode "closed" answers a socket that was bound and closed again, so connecting is refused and the list stays empty."""
    srv = socket.socket(); srv.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    srv.bind(("127.0.0.1", 0)); conns, held = [], []
    if mode == "closed":
        return srv, conns            # the caller reads the port, and the socket is never listened on
    srv.listen(8)

    def loop():
        while True:
            try: c, _ = srv.accept()
            except OSError: return
            c.settimeout(2)
            try: first = c.recv(512).split(b"\r\n")[0].decode("latin-1")
            except Exception: first = "(nothing read)"
            conns.append((time.monotonic(), first))
            if mode == "hole": held.append(c)      # never answered, never closed
            else: c.close()
    threading.Thread(target=loop, daemon=True).start()
    return srv, conns


def proxy_env(srv):
    """Points this process's HTTPS_PROXY at the listener; the game started afterwards uses it."""
    port = srv.getsockname()[1]
    os.environ["HTTPS_PROXY"] = os.environ["https_proxy"] = f"http://127.0.0.1:{port}"
    return port


def clear_proxy():
    for k in ("HTTPS_PROXY", "https_proxy", "HTTP_PROXY", "http_proxy", "ALL_PROXY", "all_proxy"): os.environ.pop(k, None)


def browser_shim(folder):
    """Puts an xdg-open first on PATH that appends its arguments to <folder>/opened.txt and opens nothing, and drops the session bus so SDL
    cannot reach the desktop portal. Answers a function that reads what was recorded."""
    shim = os.path.join(folder, "shim"); os.makedirs(shim, exist_ok=True)
    log = os.path.join(folder, "opened.txt")
    path = os.path.join(shim, "xdg-open")
    open(path, "w").write(f'#!/bin/sh\necho "$@" >> {log}\n'); os.chmod(path, 0o755)
    os.environ["PATH"] = shim + os.pathsep + os.environ["PATH"]
    os.environ.pop("DBUS_SESSION_BUS_ADDRESS", None)
    return lambda: open(log).read().split() if os.path.exists(log) else []


if __name__ == "__main__":
    srv, conns = listener("record"); port = proxy_env(srv)
    s = socket.create_connection(("127.0.0.1", port)); s.sendall(b"CONNECT example.test:443 HTTP/1.1\r\n\r\n"); s.close(); time.sleep(0.3)
    assert len(conns) == 1 and conns[0][1].startswith("CONNECT example.test:443"), conns
    with tempfile.TemporaryDirectory() as d:
        opened = browser_shim(d)
        subprocess.run(["xdg-open", "https://example.test/a"], check=True)
        assert opened() == ["https://example.test/a"], opened()
    clear_proxy(); assert "HTTPS_PROXY" not in os.environ
    print("netfake: a connection and a browser call were seen, nothing left the machine")
