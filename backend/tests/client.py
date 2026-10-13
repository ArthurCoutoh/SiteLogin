"""Integration check against the REAL ASP.NET Core app and MySQL; no mock database."""
import getpass
import http.cookiejar
import json
import os
import secrets
import sys
import urllib.error
import urllib.parse
import urllib.request

BASE=(sys.argv[1] if len(sys.argv)>1 else 'http://localhost:8080').rstrip('/')
parsed=urllib.parse.urlsplit(BASE)
if parsed.scheme!='https' and parsed.hostname not in ('localhost','127.0.0.1'):
    raise SystemExit('Use HTTPS para um servidor remoto.')
class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl): return None
class Client:
    def __init__(self):
        self.opener=urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()),NoRedirect())
    def call(self,path,body=None,csrf=True):
        headers={}
        if body is not None:
            headers['Content-Type']='application/json'
            if csrf:
                code,data=self.call('/api/auth/csrf')
                assert code==200,(code,data)
                headers['X-CSRF-TOKEN']=json.loads(data)['token']
        req=urllib.request.Request(BASE+path,data=None if body is None else json.dumps(body).encode(),headers=headers)
        try:
            with self.opener.open(req,timeout=25) as response:return response.status,response.read().decode()
        except urllib.error.HTTPError as response:return response.code,response.read().decode()

