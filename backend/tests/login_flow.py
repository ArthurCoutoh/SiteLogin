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

admin_email=os.environ.get('TEST_ADMIN_EMAIL') or input('E-mail do professor: ').strip()
admin_password=os.environ.get('TEST_ADMIN_PASSWORD') or getpass.getpass('Senha do professor: ')
student=Client();teacher=Client();anon=Client();other=Client()
email='teste-'+secrets.token_hex(6)+'@example.com';password=secrets.token_urlsafe(20)
def expect(client,path,body=None,status=200,csrf=True):
    code,data=client.call(path,body,csrf)
    assert code==status,(path,code,data)
    return json.loads(data) if data.startswith(('{','[')) else data
for path in ['/exercicios/index.html','/apoio/index.html','/entregas/index.html','/exercicios/exercicio-30/index.html']:
    expect(anon,path,status=302)
expect(anon,'/exercicios/atividades.js',status=401)
for path in ['/backend/schema.sql','/.env']:
    expect(anon,path,status=404)
for path in ['/index.html','/termos/index.html','/login/index.html']:
    expect(anon,path)
expect(anon,'/api/admin/students',status=403)
expect(anon,'/api/teacher/submissions',status=403)
expect(anon,'/api/submissions',status=401)
registration={'name':'Teste temporário','email':email,'password':password}
expect(student,'/api/auth/register',registration,status=400)
registration['acceptTerms']=True
expect(student,'/api/auth/register',registration)
expect(student,'/api/auth/login',{'email':email,'password':password})
expect(teacher,'/api/auth/login',{'email':admin_email,'password':admin_password})
uid=next(x['id'] for x in expect(teacher,'/api/admin/students') if x['email']==email)
for path in ['/exercicios/index.html','/apoio/index.html','/entregas/index.html','/exercicios/exercicio-30/index.html','/exercicios/atividades.js']:
    expect(student,path)
expect(student,'/api/admin/students',status=403)
expect(student,'/alunos/index.html',status=403)
profile=expect(student,'/api/profile');assert profile['acceptedTerms'] and not profile['shareWithTeacher']
expect(student,'/api/submissions',{'exerciseNumber':31,'title':'Inválido','solution':'x'},status=400)
expect(student,'/api/submissions',{'exerciseNumber':1,'title':'Inválido','solution':'x'*20001},status=400)
solution='<script>alert("nunca executar")</script>\nConsole.WriteLine("oi");'
sid=expect(student,'/api/submissions',{'exerciseNumber':1,'title':'Minha solução','solution':solution})['id']
assert expect(student,'/api/submissions')[0]['solution']==solution
assert all(x['id']!=sid for x in expect(teacher,'/api/teacher/submissions'))
expect(teacher,f'/api/teacher/submissions/{sid}/feedback',{'feedback':'Não permitido'},status=404)
expect(student,'/api/profile/privacy',{'shareWithTeacher':True})
assert next(x for x in expect(teacher,'/api/teacher/submissions') if x['id']==sid)['solution']==solution
expect(student,f'/api/teacher/submissions/{sid}/feedback',{'feedback':'Tentativa'},status=403)
expect(teacher,f'/api/teacher/submissions/{sid}/feedback',{'feedback':'Bom começo! Reveja a validação.'})
assert expect(student,'/api/submissions')[0]['feedback']=='Bom começo! Reveja a validação.'
other_email='teste-'+secrets.token_hex(6)+'@example.com'
expect(other,'/api/auth/register',{'name':'Outro aluno','email':other_email,'password':password,'acceptTerms':True})
expect(other,'/api/auth/login',{'email':other_email,'password':password})
assert expect(other,'/api/submissions')==[]
expect(other,f'/api/submissions/{sid}/delete',{},status=404)
expect(student,'/api/profile/privacy',{'shareWithTeacher':False})
assert all(x['id']!=sid for x in expect(teacher,'/api/teacher/submissions'))
expect(teacher,f'/api/teacher/submissions/{sid}/feedback',{'feedback':'Sem permissão'},status=404)
new_password=secrets.token_urlsafe(20)
expect(student,'/api/auth/password',{'currentPassword':password,'newPassword':new_password},status=403)
expect(anon,'/api/auth/password-request',{'email':email},csrf=False,status=400)
expect(anon,'/api/auth/password-request',{'email':email})
expect(anon,'/api/auth/password-request',{'email':email})
requests=[x for x in expect(teacher,'/api/admin/password-requests') if x['email']==email];assert len(requests)==1
rid=requests[0]['id']
expect(student,f'/api/admin/password-requests/{rid}/decision',{'decision':'approved'},status=403)
expect(anon,'/api/auth/password-reset',{'email':email,'code':'0'*32,'newPassword':new_password},status=400)
code=expect(teacher,f'/api/admin/password-requests/{rid}/decision',{'decision':'approved'})['code']
assert len(code)==32
for item in expect(teacher,'/api/admin/password-requests'):
    assert 'tokenHash' not in item and 'code' not in item
expect(anon,'/api/auth/password-reset',{'email':other_email,'code':code,'newPassword':new_password},status=400)
expect(anon,'/api/auth/password-reset',{'email':email,'code':code,'newPassword':new_password})
expect(student,'/api/auth/me',status=401)
expect(anon,'/api/auth/password-reset',{'email':email,'code':code,'newPassword':password},status=400)
expect(student,'/api/auth/login',{'email':email,'password':password},status=401)
password=new_password
expect(student,'/api/auth/login',{'email':email,'password':password})
expect(student,'/api/auth/logout',{},csrf=False,status=400)
expect(student,'/api/auth/me')
expect(student,'/api/auth/logout',{})
expect(student,'/exercicios/index.html',status=302)
expect(student,'/api/auth/login',{'email':email,'password':password})
expect(student,f'/api/submissions/{sid}/delete',{})
assert expect(student,'/api/submissions')==[]
expect(teacher,f'/api/admin/students/{uid}/status',{'status':'pending'},status=400)
expect(teacher,f'/api/admin/students/{uid}/status',{'status':'blocked'})
expect(student,'/api/auth/me',status=401)
expect(student,'/apoio/index.html',status=302)
expect(student,'/api/auth/login',{'email':email,'password':password},status=403)
expect(teacher,'/api/auth/logout',{})
print('PASS: portal MySQL, terms, immediate signup, roles, private submissions, consent/revocation, feedback, isolation, authorized one-use password reset, CSRF and bans.')
