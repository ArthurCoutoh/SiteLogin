"""Real MySQL/API checks for teacher separation, profiles, consent and private messages."""
import json, os, secrets
from client import Client

def expect(c,path,body=None,status=200):
    code,text=c.call(path,body)
    assert code==status,(path,code,text)
    return json.loads(text) if text.startswith(('{','[')) else text
anon=Client();teacher=Client()
expect(anon,'/amigos/index.html',status=302)
expect(anon,'/mensagens/index.html',status=302)
expect(anon,'/api/people?q=Teste',status=401)
expect(anon,'/api/friends',status=401)
expect(teacher,'/api/auth/login',{'email':os.environ['TEST_ADMIN_EMAIL'],'password':os.environ['TEST_ADMIN_PASSWORD']})
expect(teacher,'/entregas/index.html',status=302)
expect(teacher,'/api/submissions',status=403)
expect(teacher,'/api/submissions',{'exerciseNumber':1,'title':'Professor','solution':'x'},status=403)
expect(teacher,'/api/profile/privacy',{'shareWithTeacher':True},status=403)
accounts=[]
for label in ['A','B','C']:
    c=Client();email='social-'+secrets.token_hex(6)+'@example.com';password=secrets.token_urlsafe(20);name='Teste Social '+label+' '+secrets.token_hex(3)
    expect(c,'/api/auth/register',{'name':name,'email':email,'password':password,'acceptTerms':True})
    expect(c,'/api/auth/login',{'email':email,'password':password})
    profile=expect(c,'/api/profile');assert not profile['discoverable']
    accounts.append((c,profile['id'],name))
(a,aid,an),(b,bid,bn),(c,cid,cn)=accounts
assert expect(a,'/api/people?q='+bn.replace(' ','%20'))==[]
expect(a,f'/api/friends/{bid}/request',{},status=404)
expect(b,'/api/profile',{'name':bn,'biography':'<b>Estudando C#</b>','discoverable':True})
found=expect(a,'/api/people?q='+bn.replace(' ','%20'));assert found[0]['userId']==bid
assert all('email' not in p and 'biography' not in p for p in found)
expect(a,'/api/profile',{'name':an,'biography':'Teste','avatar':'data:image/svg+xml;base64,PHN2Zz48L3N2Zz4='},status=400)
png='data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+j5N8AAAAASUVORK5CYII='
expect(a,'/api/profile',{'name':an,'biography':'Minha biografia','avatar':png})
assert expect(a,'/api/profile')['avatar']==png
expect(c,f'/api/avatars/{aid}',status=404)
expect(a,f'/api/friends/{aid}/request',{},status=404)
expect(a,f'/api/friends/{bid}/messages',{'content':'Antes da amizade'},status=403)
expect(a,f'/api/friends/{bid}/request',{})
expect(a,f'/api/friends/{bid}/request',{},status=409)
assert next(p for p in expect(b,'/api/friends') if p['userId']==aid)['direction']=='received'
expect(a,f'/api/friends/{bid}/respond',{'decision':'accepted'},status=404)
expect(c,f'/api/friends/{bid}/respond',{'decision':'accepted'},status=404)
expect(b,f'/api/friends/{aid}/respond',{'decision':'accepted'})
assert next(p for p in expect(a,'/api/friends') if p['userId']==bid)['biography']=='<b>Estudando C#</b>'
text='<script>alert("texto literal")</script>'
expect(a,f'/api/friends/{bid}/messages',{'content':text,'senderId':cid})
messages=expect(b,f'/api/friends/{aid}/messages');assert messages[0]['content']==text and messages[0]['senderId']==aid
expect(b,f'/api/friends/{aid}/messages',{'content':'Resposta recebida'})
assert len(expect(a,f'/api/friends/{bid}/messages'))==2
assert expect(a,f'/api/friends/{bid}/messages?before='+str(messages[0]['id']))==[]
expect(c,f'/api/friends/{bid}/messages',status=403)
expect(teacher,f'/api/friends/{bid}/messages',status=403)
expect(b,f'/api/friends/{aid}/messages',{'content':' '},status=400)
expect(b,f'/api/friends/{aid}/messages',{'content':'x'*2001},status=400)
expect(b,f'/api/friends/{aid}/manage',{'action':'block'})
expect(a,f'/api/friends/{bid}/messages',status=403)
expect(a,f'/api/friends/{bid}/messages',{'content':'Após bloqueio'},status=403)
expect(a,f'/api/friends/{bid}/request',{},status=409)
assert expect(a,'/api/people?q='+bn.replace(' ','%20'))==[]
expect(a,f'/api/friends/{bid}/manage',{'action':'unblock'},status=403)
expect(b,f'/api/friends/{aid}/manage',{'action':'unblock'})
expect(a,f'/api/friends/{bid}/messages',status=403)
expect(a,f'/api/friends/{bid}/request',{})
expect(b,f'/api/friends/{aid}/respond',{'decision':'accepted'})
assert len(expect(a,f'/api/friends/{bid}/messages'))==2
expect(a,'/api/profile',{'name':an,'biography':'','removeAvatar':True})
assert expect(a,'/api/profile')['avatar'] is None
expect(teacher,f'/api/admin/students/{aid}/status',{'status':'blocked'})
expect(a,'/api/profile',status=401)
expect(b,f'/api/friends/{aid}/messages',status=404)
print('PASS: professor/student separation; private discovery; editable profiles; safe avatar types; friendship consent; message ownership, isolation and block; revocation and bans.')
