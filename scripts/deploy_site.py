#!/usr/bin/env python3
"""
Deploy Gemini Super System Showcase Site to VPS
Barrer Software • Antigravity
"""

import os
import sys
import argparse
import paramiko

NGINX_CONF_TEMPLATE = r"""# Gemini Super System Showcase // Barrer Software
server {
    listen 80;
    listen [::]:80;
    server_name geminiss.barrersoftware.com;

    root /var/www/geminiss.barrersoftware.com;
    index index.html;

    access_log /var/log/nginx/geminiss.barrersoftware.com.access.log;
    error_log /var/log/nginx/geminiss.barrersoftware.com.error.log;

    location / {
        try_files $uri $uri/ =404;
    }

    # Cache static assets
    location ~* \.(css|js|png|jpg|jpeg|gif|ico|svg|woff|woff2)$ {
        expires 7d;
        add_header Cache-Control "public, no-transform";
    }
}
"""

def main():
    parser = argparse.ArgumentParser(description="Deploy showcase site to a remote web server")
    parser.add_argument("--host", default=os.getenv("VPS_HOST"), help="VPS Hostname or IP")
    parser.add_argument("--port", type=int, default=int(os.getenv("VPS_PORT", "22")), help="SSH Port")
    parser.add_argument("--user", default=os.getenv("VPS_USER"), help="SSH Username")
    parser.add_argument("--password", default=os.getenv("VPS_PASSWORD"), help="SSH Password")
    parser.add_argument("--domain", default=os.getenv("DOMAIN", "geminiss.barrersoftware.com"), help="Domain name")
    parser.add_argument("--site-dir", default=os.path.join(os.path.dirname(__file__), "..", "site"), help="Local site folder")
    args = parser.parse_args()

    if not args.host:
        print("[-] Error: VPS host required via --host or VPS_HOST env var.", file=sys.stderr)
        sys.exit(1)
    if not args.user:
        print("[-] Error: VPS username required via --user or VPS_USER env var.", file=sys.stderr)
        sys.exit(1)
    if not args.password:
        print("[-] Error: VPS password required via --password or VPS_PASSWORD env var.", file=sys.stderr)
        sys.exit(1)

    site_dir = os.path.abspath(args.site_dir)
    if not os.path.isdir(site_dir):
        print(f"[-] Error: Site directory not found: {site_dir}", file=sys.stderr)
        sys.exit(1)

    print(f"[*] Connecting to {args.user}@{args.host}:{args.port}...")
    ssh = paramiko.SSHClient()
    ssh.set_missing_host_key_policy(paramiko.AutoAddPolicy())

    try:
        ssh.connect(
            hostname=args.host,
            port=args.port,
            username=args.user,
            password=args.password,
            timeout=15
        )
        print("[+] SSH connection established successfully.")

        def run_cmd(cmd, sudo=False):
            full_cmd = f"sudo {cmd}" if sudo else cmd
            stdin, stdout, stderr = ssh.exec_command(full_cmd)
            out = stdout.read().decode().strip()
            err = stderr.read().decode().strip()
            code = stdout.channel.recv_exit_status()
            if code != 0:
                print(f"[-] Command failed ({code}): {full_cmd}\n    Stderr: {err}")
            return code, out, err

        remote_webroot = f"/var/www/{args.domain}"
        print(f"[*] Ensuring remote directory exists: {remote_webroot}")
        run_cmd(f"mkdir -p {remote_webroot}", sudo=True)
        run_cmd(f"chown -R {args.user}:{args.user} {remote_webroot}", sudo=True)

        sftp = ssh.open_sftp()
        print(f"[*] Uploading files from {site_dir} to {remote_webroot}...")
        for fname in os.listdir(site_dir):
            local_path = os.path.join(site_dir, fname)
            if os.path.isfile(local_path):
                remote_path = f"{remote_webroot}/{fname}"
                print(f"    -> Uploading {fname} ({os.path.getsize(local_path)} bytes)...")
                sftp.put(local_path, remote_path)
        sftp.close()
        print("[+] All static assets uploaded successfully.")

        # Ensure correct file permissions
        run_cmd(f"chmod -R 755 {remote_webroot}", sudo=True)
        run_cmd(f"chown -R www-data:www-data {remote_webroot}", sudo=True)

        # Configure Nginx
        nginx_avail = f"/etc/nginx/sites-available/{args.domain}"
        nginx_enabled = f"/etc/nginx/sites-enabled/{args.domain}"
        print(f"[*] Writing Nginx site config: {nginx_avail}")

        # Write config via temporary file in /tmp then move with sudo
        tmp_conf = f"/tmp/nginx_{args.domain}.conf"
        sftp = ssh.open_sftp()
        with sftp.file(tmp_conf, "w") as f:
            f.write(NGINX_CONF_TEMPLATE)
        sftp.close()

        run_cmd(f"mv {tmp_conf} {nginx_avail}", sudo=True)
        run_cmd(f"ln -sf {nginx_avail} {nginx_enabled}", sudo=True)

        print("[*] Testing Nginx configuration syntax...")
        code, out, err = run_cmd("nginx -t", sudo=True)
        if code != 0:
            print(f"[-] Nginx syntax test failed:\n{err}")
            sys.exit(1)
        print(f"[+] Nginx test passed: {out or err}")

        print("[*] Reloading Nginx service...")
        code, out, err = run_cmd("systemctl reload nginx", sudo=True)
        if code != 0:
            print(f"[-] Failed to reload Nginx: {err}")
            sys.exit(1)
        print("[+] Nginx reloaded successfully!")

        print("\n========================================================")
        print(f"[OK] SUCCESS: Showcase site deployed to http://{args.domain}")
        print(f"   Root: {remote_webroot}")
        print(f"   Server: {args.host}")
        print("   Note: Ensure DNS record (CNAME or A) points to your host.")
        print(f"   To enable HTTPS once DNS propagates: sudo certbot --nginx -d {args.domain}")
        print("========================================================")

    finally:
        ssh.close()

if __name__ == "__main__":
    main()
