"""HTTP boundary and persistent multi-client records on PostgreSQL, without a webcam or private DB."""
import http.client
import json
import queue
import threading
import unittest
import uuid
from server import create_server
from test_identity import PostgresCase


class HttpTests(PostgresCase):
    def setUp(self):
        super().setUp()
        self.start_server()

    def start_server(self):
        ready=queue.Queue()
        def run():
            store=self.make_store()
            with store.db:
                store.db.execute('INSERT INTO profiles (id,name,name_key,coins,created_at,consent_version) VALUES(?,?,?,?,?,?) ON CONFLICT DO NOTHING',
                                 ('visitor','Visitor','visitor',40,1,'test'))
            token=store.issue('visitor')
            # No face extraction is used here; real model coverage lives in test_identity.py.
            server=create_server('127.0.0.1',0,store,None,'faces.test,127.0.0.1:8765')
            ready.put((server,token))
            try: server.serve_forever(poll_interval=.02)
            finally: server.server_close(); store.db.close()
        self.thread=threading.Thread(target=run,daemon=True)
        self.thread.start()
        self.server,self.token=ready.get(timeout=10)

    def stop_server(self):
        self.server.shutdown(); self.thread.join(timeout=5)
        self.assertFalse(self.thread.is_alive())

    def tearDown(self):
        self.stop_server()

    def request(self,path,body=None,host='faces.test',headers=None,token=None):
        connection=http.client.HTTPConnection('127.0.0.1',self.server.server_port,timeout=5)
        fields={'Host':host,'Content-Type':'application/json'}
        if token: fields['Authorization']='Bearer '+token
        fields.update(headers or {})
        connection.request('GET' if body is None else 'POST',path,body=body,headers=fields)
        response=connection.getresponse()
        result=(response.status,dict(response.getheaders()),json.loads(response.read()))
        connection.close()
        return result

    def test_health_and_unrecognized_routes(self):
        status,headers,data=self.request('/health')
        self.assertEqual(status,200);self.assertTrue(data['ok']);self.assertEqual(data['storage'],'postgres')
        self.assertEqual(headers['Cache-Control'],'no-store')
        self.assertNotIn('database',data)
        self.assertEqual(self.request('/missing')[0],404)

    def test_explicit_proxy_host_allowed_unknown_host_and_browser_rejected(self):
        self.assertEqual(self.request('/history','{}',token=self.token)[0],200)
        self.assertEqual(self.request('/history','{}',host='attacker.test',token=self.token)[0],403)
        self.assertEqual(self.request('/history','{}',headers={'Origin':'https://faces.test'},token=self.token)[0],403)

    def test_rate_limit_follows_the_proxy_client_address(self):
        check=lambda client:self.request('/name-check',json.dumps(dict(name='free')),headers={'X-Forwarded-For':client})[0]
        self.assertEqual([check('198.51.100.7') for _ in range(60)],[200]*60)
        self.assertEqual(check('198.51.100.7'),429)
        self.assertEqual(check('203.0.113.9'),200) # another player keeps their own budget
        self.assertEqual(check('203.0.113.9, 198.51.100.7'),429) # only the last (proxy-observed) entry counts

    def test_payload_limits_types_and_authentication(self):
        self.assertEqual(self.request('/history','{}')[0],401)
        self.assertEqual(self.request('/history','{}',headers={'Content-Type':'text/plain'})[0],415)
        self.assertEqual(self.request('/history','[]')[0],400)
        self.assertEqual(self.request('/history','{')[0],400)
        self.assertEqual(self.request('/history','{}',headers={'Content-Length':str(5*1024*1024)})[0],413)

    def test_two_clients_share_records_after_server_restart(self):
        record=dict(id=str(uuid.uuid4()),distance=100,score=25,kcal=2,goal=50,activities=[1,1,0,0,0],mode=1,coins=3,play_seconds=30,score_version=2)
        for _ in range(2):  # a retried record id credits its coins once
            self.assertEqual(self.request('/runs',json.dumps(record),token=self.token)[::2],(200,dict(ok=True,coins=43)))
        original_token=self.token
        self.stop_server();self.start_server()
        status,_,data=self.request('/history','{}',token=original_token)
        self.assertEqual(status,200);self.assertEqual(data['runs'][0]['score'],25)
        self.assertEqual(self.request('/delete','{}',token=self.token)[0],200)
        self.assertEqual(self.request('/history','{}',token=original_token)[0],401)

    def test_account_routes_status_codes(self):
        def call(path,body,token=None):
            status,_,data=self.request(path,json.dumps(body),token=token);return status,data
        self.assertEqual(call('/name-check',dict(name='VISITOR')),(200,dict(ok=True,available=False,message='이미 사용 중인 닉네임입니다.')))
        status,data=call('/profile',{},self.token)
        self.assertEqual((status,data['profile']['coins'],data['profile']['unlocked']),(200,40,[]))
        self.assertEqual(call('/profile',{})[0],401)
        self.assertEqual(call('/profile/update',dict(name='12345678901'),self.token)[0],400)
        self.assertEqual(call('/profile/update',dict(name='Guest',style=2),self.token)[1]['profile']['style'],2)
        self.assertEqual(call('/shop/buy',dict(item=2),self.token)[0],400)
        self.assertEqual(call('/shop/buy',dict(item=0),self.token)[1]['profile']['coins'],10)
        self.assertEqual(call('/shop/buy',dict(item=0),self.token)[0],409)
        self.assertEqual(call('/leaderboard',dict(mode=9))[0],400)
        self.assertEqual(call('/leaderboard',dict(mode=0,today=True)),(200,dict(ok=True,entries=[])))
        self.assertEqual(call('/stats',{},self.token),(200,dict(ok=True,today_kcal=0,total_kcal=0,best_score=[0,0,0])))

    def test_wildcard_hosts_rejected(self):
        with self.assertRaises(ValueError):create_server('127.0.0.1',0,None,None,'*')


if __name__=='__main__':unittest.main(verbosity=2)
