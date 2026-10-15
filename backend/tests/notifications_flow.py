"""Real MySQL checks for notification ownership, deep links and independent delivery visibility."""
import json, os, secrets, time
from client import Client

def call(c,path,body=None,status=200):
    for attempt in range(35):
        code,text=c.call(path,body)
        if code!=429: break
        time.sleep(2)
    assert code==status,(path,code,text)
    return json.loads(text) if text.startswith(('{','[')) else text

anon=Client(); teacher=Client()
call(anon,'/api/notifications',status=401)
call(anon,'/api/notifications/read-all',{},status=401)
call(anon,'/perfil/index.html',status=302)
call(teacher,'/api/auth/login',{'email':os.environ['TEST_ADMIN_EMAIL'],'password':os.environ['TEST_ADMIN_PASSWORD']})
accounts=[]
for label in ['Autor','Leitor','Isolado']:
    c=Client();email='notify-'+secrets.token_hex(6)+'@example.com';password=secrets.token_urlsafe(20)
    call(c,'/api/auth/register',{'name':label,'email':email,'password':password,'acceptTerms':True})
    call(c,'/api/auth/login',{'email':email,'password':password})
    p=call(c,'/api/profile')
    call(c,'/api/profile',{'name':label,'biography':'Teste','discoverable':True})
    accounts.append((c,p['id'],email))
(a,aid,ae),(b,bid,be),(c,cid,ce)=accounts
call(a,f'/api/friends/{bid}/request',{})
pending=next(x for x in call(b,'/api/notifications') if x['kind']=='friend')
assert pending['url'].endswith('aba=pending&pessoa='+str(aid))
assert not pending['read']
call(c,'/api/notifications/read',{'id':pending['id']},404)
call(b,'/api/notifications/read',{'id':pending['id']})
assert next(x for x in call(b,'/api/notifications') if x['id']==pending['id'])['read']
call(b,f'/api/friends/{aid}/respond',{'decision':'accepted'})
call(a,f'/api/friends/{bid}/messages',{'content':'Mensagem de teste'})
msg=next(x for x in call(b,'/api/notifications') if x['kind']=='message')
assert 'amigo='+str(aid) in msg['url'] and '&mensagem=' in msg['url']
assert not any(x['kind']=='message' for x in call(c,'/api/notifications'))
assert not any(x['kind']=='message' for x in call(teacher,'/api/notifications'))
other_before=call(a,'/api/notifications')
marked=call(b,'/api/notifications/read-all',{})
assert msg['id'] in marked['ids']
assert all(x['read'] for x in call(b,'/api/notifications'))
assert call(a,'/api/notifications')==other_before
assert set(call(b,'/api/notifications/read-all',{})['ids'])==set(marked['ids'])
submission=call(a,'/api/submissions',{'exerciseNumber':1,'title':'Teste de visibilidade','solution':'Console.WriteLine("<b>literal</b>");'})['id']
assert not call(b,f'/api/people/{aid}/profile')['canViewSubmissions']
call(b,f'/api/submissions/{submission}/comments',{'content':'Não autorizado'},404)
call(a,'/api/profile/privacy',{'shareWithTeacher':False,'shareWithStudents':True,'acceptTerms':True})
saved=call(a,'/api/profile')
assert saved['shareWithStudents'] and not saved['shareWithTeacher']
assert call(b,f'/api/people/{aid}/profile')['canViewSubmissions']
assert not call(teacher,f'/api/people/{aid}/profile')['canViewSubmissions']
call(b,f'/api/submissions/{submission}/comments',{'content':'<script>texto literal</script>'})
shared=call(c,f'/api/people/{aid}/profile')
assert shared['comments'][0]['content']=='<script>texto literal</script>'
assert 'email' not in shared and 'email' not in shared['comments'][0]
peer=next(x for x in call(a,'/api/notifications') if x['id'].startswith('peer:'))
assert 'entrega='+str(submission) in peer['url']
call(a,f'/api/submissions/{submission}/comments',{'content':'Meu próprio feedback'},404)
call(a,'/api/profile/privacy',{'shareWithTeacher':True,'shareWithStudents':False})
saved=call(a,'/api/profile')
assert saved['shareWithTeacher'] and not saved['shareWithStudents']
assert call(b,f'/api/people/{aid}/profile')['submissions']==[]
call(b,f'/api/submissions/{submission}/comments',{'content':'Revogado'},404)
assert any(x['id']=='submission:'+str(submission) for x in call(teacher,'/api/notifications'))
call(teacher,f'/api/teacher/submissions/{submission}/feedback',{'feedback':'Bom trabalho'})
feedback=next(x for x in call(a,'/api/notifications') if x['id'].startswith('feedback:'+str(submission)+':'))
assert feedback['url'].endswith('entrega='+str(submission))
call(a,'/api/auth/password-request',{'email':ae})
reset=next(x for x in call(teacher,'/api/notifications') if x['kind']=='password' and x['title'].startswith('Autor'))
assert '?aba=requests&item=' in reset['url']
call(b,f'/api/friends/{aid}/manage',{'action':'block'})
assert not any(x['kind'] in ['message','friend'] for x in call(b,'/api/notifications'))
call(b,f'/api/people/{aid}/profile',status=404)
call(teacher,f'/api/admin/students/{aid}/status',{'status':'blocked'})
call(a,'/api/notifications',status=401)
print('PASS: notification isolation/read state/deep links; independent persistent visibility; peer feedback; revocation; block; ban.')
