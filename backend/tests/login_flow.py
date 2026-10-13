"""Integration check against the REAL ASP.NET Core app and MySQL; no mock database."""
import getpass
import http.cookiejar
import json
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
admin_email=input('E-mail do professor: ').strip()
admin_password=getpass.getpass('Senha do professor (não será exibida): ')
student=Client();teacher=Client();anon=Client()
email='teste-'+secrets.token_hex(6)+'@example.com';password=secrets.token_urlsafe(20)
for path in ['/exercicios/index.html','/apoio/index.html','/exercicios/exercicio-30/index.html']:
    assert anon.call(path)[0]==302,path
assert anon.call('/exercicios/atividades.js')[0]==401
assert anon.call('/backend/schema.sql')[0]==404
assert anon.call('/.env')[0]==404
assert anon.call('/index.html')[0]==200
assert anon.call('/api/admin/students')[0]==403
assert student.call('/api/auth/register',{'name':'Teste temporário','email':email,'password':password})[0]==200
assert student.call('/api/auth/login',{'email':email,'password':password})[0]==403
assert teacher.call('/api/auth/login',{'email':admin_email,'password':admin_password})[0]==200,'Login do professor falhou.'
code,body=teacher.call('/api/admin/students');assert code==200
account=next(x for x in json.loads(body) if x['email']==email);uid=account['id']
assert teacher.call(f'/api/admin/students/{uid}/status',{'status':'active'})[0]==200
assert student.call('/api/auth/login',{'email':email,'password':password})[0]==200
for path in ['/exercicios/index.html','/apoio/index.html','/exercicios/exercicio-30/index.html','/exercicios/atividades.js']:
    assert student.call(path)[0]==200,path
new_password=secrets.token_urlsafe(20)
assert student.call('/api/auth/password',{'currentPassword':password,'newPassword':new_password})[0]==200
assert student.call('/api/auth/me')[0]==401
assert student.call('/api/auth/login',{'email':email,'password':password})[0]==401
password=new_password
assert student.call('/api/auth/login',{'email':email,'password':password})[0]==200
assert student.call('/api/admin/students')[0]==403
assert student.call('/alunos/index.html')[0]==403
assert student.call('/api/auth/logout',{},csrf=False)[0]==400,'Logout sem CSRF deveria falhar.'
assert student.call('/api/auth/me')[0]==200
assert student.call('/api/auth/logout',{})[0]==200
assert student.call('/exercicios/index.html')[0]==302
assert student.call('/api/auth/login',{'email':email,'password':password})[0]==200
assert teacher.call(f'/api/admin/students/{uid}/status',{'status':'blocked'})[0]==200
assert student.call('/api/auth/me')[0]==401
assert student.call('/apoio/index.html')[0]==302
assert student.call('/api/auth/login',{'email':email,'password':password})[0]==403
assert teacher.call('/api/auth/logout',{})[0]==200
print('PASS: public portfolio, protected HTML/JS, pending signup, admin approval, student permissions, CSRF, logout and revoked access.')
print('Conta de teste bloqueada:',email)
