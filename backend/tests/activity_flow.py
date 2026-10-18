"""Activity checks against isolated CI MySQL only. Never run this test on production."""
import json, os, secrets, subprocess, urllib.parse, time
from client import Client, BASE
assert urllib.parse.urlsplit(BASE).hostname in ('localhost','127.0.0.1'), 'CI/local only'
def call(c,path,body=None,status=200,csrf=True):
    for attempt in range(35):
        code,text=c.call(path,body,csrf=csrf)
        if code!=429: break
        time.sleep(2)
    assert code==status,(path,code,text)
    return json.loads(text) if text.startswith(('{','[')) else text
def sql(query):
    env=os.environ.copy();env['MYSQL_PWD']=env['MYSQLPASSWORD']
    return subprocess.check_output(['docker','exec','--env','MYSQL_PWD',env['MYSQL_TEST_CONTAINER'],'mysql','-u',env['MYSQLUSER'],env['MYSQLDATABASE'],'--batch','--skip-column-names','-e',query],env=env,text=True).strip()
anon=Client();teacher=Client();student=Client()
call(anon,'/api/activity/presence',{},401)
call(anon,'/api/admin/activity/users',status=403)
call(teacher,'/api/auth/login',{'email':os.environ['TEST_ADMIN_EMAIL'],'password':os.environ['TEST_ADMIN_PASSWORD']})
email='activity-'+secrets.token_hex(6)+'@example.com';password=secrets.token_urlsafe(20)
call(student,'/api/auth/register',{'name':'Teste Atividade','email':email,'password':password,'acceptTerms':True})
call(student,'/api/auth/login',{'email':email,'password':password})
uid=call(student,'/api/auth/me')['id']
call(student,'/api/admin/activity/users',status=403)
call(student,f'/api/admin/activity/users/{uid}',status=403)
call(student,'/api/activity/presence',{},400,False)
call(student,'/exercicios/exercicio-1/index.html?segredo=NAO_REGISTRAR')
call(student,'/api/activity/presence',{})
data=call(teacher,f'/api/admin/activity/users/{uid}')
assert any(e['type']=='login' for e in data['events'])
assert any(e['path']=='/exercicios/exercicio-1/index.html' for e in data['events'])
assert 'NAO_REGISTRAR' not in json.dumps(data)
assert 'session_key' not in json.dumps(data) and 'token_hash' not in json.dumps(data)
assert data['sessions'][0]['online'] and data['sessions'][0]['loginAt'].endswith('Z')
rows=call(teacher,'/api/admin/activity/users?q='+email)['users']
assert len(rows)==1 and rows[0]['online']
sql(f"UPDATE activity_sessions SET last_seen_at=UTC_TIMESTAMP()-INTERVAL 10 SECOND WHERE user_id={int(uid)} AND logout_at IS NULL")
call(student,'/api/activity/presence',{})
seconds=call(teacher,f'/api/admin/activity/users/{uid}')['sessions'][0]['onlineSeconds']
assert 9<=seconds<=15,seconds
sql(f"UPDATE activity_sessions SET last_seen_at=UTC_TIMESTAMP()-INTERVAL 120 SECOND WHERE user_id={int(uid)} AND logout_at IS NULL")
assert not call(teacher,f'/api/admin/activity/users/{uid}')['sessions'][0]['online']
assert not call(teacher,'/api/admin/activity/users?q='+email)['users'][0]['online']
call(student,'/api/activity/presence',{})
resumed=call(teacher,f'/api/admin/activity/users/{uid}')['sessions'][0]
assert resumed['online'] and resumed['onlineSeconds']==seconds
call(student,'/api/auth/logout',{})
data=call(teacher,f'/api/admin/activity/users/{uid}')
assert data['sessions'][0]['exitReason']=='logout' and data['sessions'][0]['logoutAt'].endswith('Z')
assert not data['sessions'][0]['online']
assert any(e['type']=='logout' for e in data['events'])
call(student,'/api/auth/login',{'email':email,'password':password})
for i in range(52):
    call(student,'/index.html?omit='+str(i))
data=call(teacher,f'/api/admin/activity/users/{uid}')
assert len(data['sessions'])==2 and len(data['events'])==50 and data['nextBefore']
older=call(teacher,f"/api/admin/activity/users/{uid}?before={data['nextBefore']}")
assert older['events'] and not set(e['id'] for e in data['events']) & set(e['id'] for e in older['events'])
call(teacher,f'/api/admin/students/{uid}/status',{'status':'blocked'})
assert not call(teacher,'/api/admin/activity/users?q='+email)['users'][0]['online']
call(student,'/api/activity/presence',{},401)
print('PASS: activity access control, server page paths, no URL parameters, UTC timestamps, presence timeout, duration, logout, sessions, pagination and bans.')
