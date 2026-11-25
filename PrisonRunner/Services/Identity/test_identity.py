"""Identity server tests against a real PostgreSQL. Each test runs in its own generated schema of the
MONGSCAPE_TEST_POSTGRES_URL database (a non-admin app role, as in Docker); run_tests.ps1 provides one."""
import base64,os,sys,tempfile,time,unittest,urllib.request,uuid
from datetime import datetime,timezone
from pathlib import Path
from unittest import mock
BASE=Path(__file__).resolve().parent
sys.path.insert(0,str(BASE/'.runtime'))
from server import App,Store,Faces,ApiError,kst_midnight
from storage import SCHEMA_VERSION,ConfigError,SchemaError
from prepare_secrets import prepare
import migrate_account
URL=os.getenv('MONGSCAPE_TEST_POSTGRES_URL')
NO_URL='MONGSCAPE_TEST_POSTGRES_URL is not set; run run_tests.ps1, or point it at an isolated test database'
PROFILE_INSERT='INSERT INTO profiles (id,name,name_key,created_at,consent_version) VALUES(?,?,?,?,?)'
@unittest.skipUnless(URL,NO_URL)
class PostgresCase(unittest.TestCase):
 """Creates a mongscape_test_<hex> schema per test and drops it afterwards; nothing else in the DB is touched."""
 def setUp(self):
  import psycopg
  self.psycopg=psycopg;self.admin=psycopg.connect(URL,autocommit=True);self.addCleanup(self.admin.close)
  self.schema='mongscape_test_'+uuid.uuid4().hex
  self.admin.execute(psycopg.sql.SQL('CREATE SCHEMA {}').format(psycopg.sql.Identifier(self.schema)))
  self.addCleanup(self.drop_schema) # cleanups run after tearDown has closed the store
  self.url=psycopg.conninfo.make_conninfo(URL,options='-c search_path='+self.schema)
 def drop_schema(self):
  assert self.schema.startswith('mongscape_test_') and len(self.schema)==47
  self.admin.execute(self.psycopg.sql.SQL('DROP SCHEMA {} CASCADE').format(self.psycopg.sql.Identifier(self.schema)))
 def make_store(self):return Store(database_url=self.url)
# Schema v1 before accounts, still holding stored photos (profiles.avatar); used to test the upgrade.
V1='''CREATE TABLE profiles (id TEXT PRIMARY KEY, name TEXT NOT NULL, avatar BYTEA NOT NULL, created_at BIGINT NOT NULL, consent_version TEXT NOT NULL);
CREATE TABLE face_samples (profile_id TEXT NOT NULL REFERENCES profiles(id) ON DELETE CASCADE, embedding BYTEA NOT NULL, PRIMARY KEY (profile_id, embedding));
CREATE TABLE sessions (token_hash TEXT PRIMARY KEY, profile_id TEXT NOT NULL REFERENCES profiles(id) ON DELETE CASCADE, expires_at BIGINT NOT NULL);
CREATE TABLE runs (id TEXT PRIMARY KEY, profile_id TEXT NOT NULL REFERENCES profiles(id) ON DELETE CASCADE, distance DOUBLE PRECISION NOT NULL CHECK (distance >= 0),
 score BIGINT NOT NULL CHECK (score >= 0), kcal DOUBLE PRECISION NOT NULL CHECK (kcal >= 0), goal DOUBLE PRECISION NOT NULL CHECK (goal > 0),
 activities JSONB NOT NULL CHECK (jsonb_typeof(activities) = 'array' AND jsonb_array_length(activities) = 5), created_at BIGINT NOT NULL);
CREATE INDEX runs_profile_created ON runs (profile_id, created_at DESC);
CREATE INDEX sessions_expiry ON sessions (expires_at);
CREATE INDEX sessions_profile ON sessions (profile_id);'''
def run(**fields):return dict(dict(id=str(uuid.uuid4()),distance=100,score=100,kcal=1,goal=50,activities=[1,0,0,0,0],mode=0,coins=0,play_seconds=60,score_version=2),**fields)
class IdentityTests(PostgresCase):
 @classmethod
 def setUpClass(cls):
  import cv2,numpy as np
  cls.cv,cls.np=cv2,np;cls.faces=Faces(BASE/'models')
  raw=urllib.request.urlopen('https://raw.githubusercontent.com/opencv/opencv/master/samples/data/lena.jpg',timeout=20).read()
  cls.image=cv2.imdecode(np.frombuffer(raw,np.uint8),1)
  cls.images=[]
  for degree in (0,3,-3):
   m=cv2.getRotationMatrix2D((256,256),degree,1);image=cv2.warpAffine(cls.image,m,(512,512));_,jpg=cv2.imencode('.jpg',image)
   cls.images.append(base64.b64encode(jpg).decode())
 def setUp(self):
  super().setUp();self.store=self.make_store();self.app=App(self.store,self.faces)
 def tearDown(self):self.store.db.close()
 def enroll(self,**fields):return self.app.handle('/enroll',dict(dict(name='Tester',consent=True,images=self.images),**fields))
 def member(self,name,coins=0,created=1):
  """Account without face samples, for routes that only need a session."""
  pid=uuid.uuid4().hex
  with self.store.db:self.store.db.execute('INSERT INTO profiles (id,name,name_key,coins,created_at,consent_version) VALUES(?,?,?,?,?,?)',
   (pid,name,''.join(name.split()).casefold(),coins,created,'test'))
  return pid,self.store.issue(pid)
 def record(self,pid,score,mode=0,version=2,created=None,kcal=1.0):
  with self.store.db:self.store.db.execute('INSERT INTO runs (id,profile_id,distance,score,kcal,goal,activities,created_at,mode,score_version) VALUES(?,?,?,?,?,?,?,?,?,?)',
   (str(uuid.uuid4()),pid,score/2,score,kcal,50,'[0,0,0,0,0]',int(time.time()) if created is None else created,mode,version))
 def status(self,path,data,token=''):
  with self.assertRaises(ApiError) as caught:self.app.handle(path,data,token)
  return caught.exception.status
 def test_real_model_enroll_reopen_login_record_delete(self):
  first=self.enroll(weight=72.5,style=1,goal=30);self.assertTrue(first['ok']);self.assertTrue(first['avatar']);self.assertNotEqual(first['token'],'')
  self.assertEqual(first['profile'],dict(id=first['id'],name='Tester',weight=72.5,style=1,goal=30.0,coins=0,unlocked=[],tutorial_done=False))
  self.assertNotIn('avatar',self.store.columns('profiles'))
  self.assertEqual(self.store.db.execute('SELECT consent_version FROM profiles').fetchone()[0],'account-v3')
  self.store.db.close();self.store=self.make_store();self.app=App(self.store,self.faces)
  login=self.app.handle('/login',dict(images=self.images));self.assertEqual(first['id'],login['profile']['id']);self.assertNotEqual(first['token'],login['token'])
  # The badge photo comes from this request's frames, so it is returned although nothing was stored.
  self.assertEqual(base64.b64decode(login['avatar'])[:2],b'\xff\xd8')
  record=run(score=567,coins=7)
  self.app.handle('/runs',record,login['token']);self.app.handle('/runs',record,login['token'])
  history=self.app.handle('/history',{},login['token']);self.assertEqual(len(history['runs']),1)
  self.assertEqual(history['runs'][0]['score'],567)
  self.app.handle('/delete',{},login['token'])
  for table in ('profiles','face_samples','runs','sessions'):self.assertEqual(self.store.db.execute('SELECT COUNT(*) FROM '+table).fetchone()[0],0)
  with self.assertRaises(ApiError):self.store.authorize(login['token'])
 def test_consent_duplicate_unknown_and_logout(self):
  with self.assertRaises(ApiError):self.app.handle('/enroll',dict(name='Tester',consent=False,images=self.images))
  self.assertEqual(self.status('/login',dict(images=self.images)),401)
  p=self.enroll()
  self.assertEqual(self.status('/enroll',dict(name='Other',consent=True,images=self.images)),409) # same face
  with self.assertRaises(ApiError):self.app.handle('/history',{},'wrong token')
  self.app.handle('/logout',{},p['token'])
  with self.assertRaises(ApiError):self.store.authorize(p['token'])
 def test_enroll_checks_nickname_and_settings(self):
  self.member('Min Su')
  self.assertEqual(self.status('/enroll',dict(name='minsu',consent=True,images=self.images)),409)
  for name in ('',' ','12345678901','a\x07b',None,5):self.assertEqual(self.status('/enroll',dict(name=name,consent=True,images=self.images)),400,name)
  for fields in (dict(weight=0),dict(weight='65'),dict(style=3),dict(style=1.0),dict(goal=0)):self.assertEqual(self.status('/enroll',dict(name='Tester',consent=True,images=self.images,**fields)),400,fields)
  self.assertEqual(self.store.db.execute('SELECT COUNT(*) FROM face_samples').fetchone()[0],0)
  profile=self.enroll(name='  Tester ',weight=None)['profile'];self.assertEqual((profile['name'],profile['weight'],profile['goal']),('Tester',65.0,50.0))
 def test_name_check_rules_and_limit(self):
  self.member('Min Su')
  check=lambda name:self.app.handle('/name-check',dict(name=name))
  self.assertEqual(check('민수'),dict(ok=True,available=True,message='사용할 수 있는 닉네임입니다.'))
  self.assertTrue(check(' 1234567890 ')['available'])
  self.assertTrue(check('\U0001F600'*10)['available']) # code points, as the game's AccountSession.NicknameProblem
  for name in ('minsu','MIN SU',' Min  Su ','',' ','12345678901','\U0001F600'*11,'a\nb','a​b','ab',None,7):
   result=check(name);self.assertTrue(result['ok']);self.assertFalse(result['available'],name);self.assertTrue(result['message'])
  for _ in range(60-15):check('free')
  self.assertEqual(self.status('/name-check',dict(name='free')),429)
  self.assertFalse(any(key[0]=='face' for key in self.app.attempts)) # separate budget from face enroll/login
  self.assertTrue(self.app.handle('/name-check',dict(name='free'),client='10.0.0.2')['available']) # other IPs keep their budget
 def test_limits_are_per_client_with_server_cap(self):
  for i in range(15):self.app.limit(('face','10.0.0.1'),15)
  with self.assertRaises(ApiError) as caught:self.app.handle('/login',dict(images=[]),client='10.0.0.1')
  self.assertEqual(caught.exception.status,429)
  with self.assertRaises(ApiError) as caught:self.app.handle('/login',dict(images=[]),client='10.0.0.2')
  self.assertEqual(caught.exception.status,400) # reached face checks: not locked out by 10.0.0.1
  self.app.attempts[('face',)]=[time.monotonic()]*60 # server-wide cap bounds inference CPU
  with self.assertRaises(ApiError) as caught:self.app.handle('/login',dict(images=[]),client='10.0.0.3')
  self.assertEqual(caught.exception.status,429)
  _,token=self.member('Runner')
  for _ in range(20):self.app.handle('/runs',run(),token)
  self.assertEqual(self.status('/runs',run(),token),429)
 def test_profile_and_update(self):
  pid,token=self.member('Alpha');self.member('Beta')
  self.assertEqual(self.status('/profile',{}),401);self.assertEqual(self.status('/profile/update',dict(name='Gamma'),'bad'),401)
  self.assertEqual(self.app.handle('/profile',{},token)['profile'],dict(id=pid,name='Alpha',weight=65.0,style=0,goal=50.0,coins=0,unlocked=[],tutorial_done=False))
  self.assertEqual(self.status('/profile/update',dict(name='beta'),token),409)
  for fields in (dict(name=''),dict(name='12345678901'),dict(weight=-1),dict(weight=9.9),dict(weight=351),dict(style=2.5),dict(goal=6000),dict(tutorial_done=1)):
   self.assertEqual(self.status('/profile/update',fields,token),400,fields)
  with self.assertRaisesRegex(ApiError,'10~350kg'):self.app.handle('/profile/update',dict(weight=500),token) # the game's 10-350 kg rule
  self.assertEqual([self.app.handle('/profile/update',dict(weight=w),token)['profile']['weight'] for w in (10,350)],[10.0,350.0])
  self.assertEqual(self.app.handle('/profile/update',dict(name='ALPHA '),token)['profile']['name'],'ALPHA') # own name, new case
  profile=self.app.handle('/profile/update',dict(weight=80,style=2,tutorial_done=True),token)['profile']
  self.assertEqual((profile['name'],profile['weight'],profile['style'],profile['goal'],profile['tutorial_done']),('ALPHA',80.0,2,50.0,True))
  self.assertEqual(self.app.handle('/profile/update',dict(weight=None),token)['profile']['weight'],65.0)
  self.assertEqual(self.app.handle('/profile/update',{},token)['profile']['style'],2)
  self.assertEqual(self.app.handle('/profile/update',dict(name='Gamma'),token)['profile']['name'],'Gamma')
  self.assertTrue(self.app.handle('/name-check',dict(name='alpha'))['available'])
 def test_runs_credit_coins_once_and_validate(self):
  pid,token=self.member('Runner',coins=3)
  self.assertEqual(self.status('/runs',run(),''),401)
  first=run(coins=12)
  self.assertEqual(self.app.handle('/runs',first,token),dict(ok=True,coins=15))
  self.assertEqual(self.app.handle('/runs',first,token),dict(ok=True,coins=15)) # resend: stored and credited once
  self.assertEqual(self.app.handle('/runs',run(coins=5,mode=2,score_version=1,play_seconds=0),token),dict(ok=True,coins=20))
  for fields in (dict(mode=3),dict(mode=-1),dict(mode='0'),dict(mode=None),dict(mode=True),dict(coins=5001),dict(coins=1.5),dict(coins=-1),
                 dict(score_version=3),dict(score_version=0),dict(play_seconds=86401),dict(play_seconds=-1),dict(id='x'),dict(goal=0),dict(activities=[1])):
   self.assertEqual(self.status('/runs',run(**fields),token),400,fields)
  legacy=run();[legacy.pop(k) for k in ('mode','coins','play_seconds','score_version')]
  self.assertEqual(self.app.handle('/runs',legacy,token)['coins'],20) # clients before accounts still save, unranked
  rows=self.store.db.execute('SELECT mode,coins,score_version,play_seconds FROM runs').fetchall()
  self.assertEqual(set(map(tuple,rows)),{(0,12,2,60),(2,5,1,0),(None,0,1,0)})
 def test_shop_buy(self):
  pid,token=self.member('Shopper',coins=100)
  self.assertEqual(self.status('/shop/buy',dict(item=0)),401)
  profile=self.app.handle('/shop/buy',dict(item=0),token)['profile'];self.assertEqual((profile['coins'],profile['unlocked']),(70,[0]))
  self.assertEqual(self.status('/shop/buy',dict(item=0),token),409)
  self.assertEqual(self.status('/shop/buy',dict(item=2),token),400) # 100 coins needed
  for item in (3,-1,'1',True,None,1.0):self.assertEqual(self.status('/shop/buy',dict(item=item),token),400,item)
  profile=self.app.handle('/shop/buy',dict(item=1),token)['profile'];self.assertEqual((profile['coins'],profile['unlocked']),(10,[0,1]))
  self.assertEqual(self.app.handle('/profile',{},token)['profile']['unlocked'],[0,1])
 def test_leaderboard_best_per_member(self):
  a,ta=self.member('Alpha');b,tb=self.member('Beta');c,_=self.member('Gamma');_,td=self.member('Delta')
  self.record(a,100,created=10);self.record(a,300,created=20);self.record(b,300,created=5) # tie: earlier record first
  self.record(c,999,version=1);self.record(c,50,mode=1);self.record(b,10,mode=2)
  board=self.app.handle('/leaderboard',dict(mode=0,today=False),ta)
  self.assertEqual(board['entries'],[dict(rank=1,name='Beta',score=300,distance=150.0,created_at=5),dict(rank=2,name='Alpha',score=300,distance=150.0,created_at=20)])
  self.assertEqual(board['me'],dict(rank=2,score=300,distance=150.0))
  self.assertNotIn('me',self.app.handle('/leaderboard',dict(mode=0),''));self.assertNotIn('me',self.app.handle('/leaderboard',dict(mode=0),'expired'))
  self.assertNotIn('me',self.app.handle('/leaderboard',dict(mode=0),td)) # no record
  self.assertEqual([e['name'] for e in self.app.handle('/leaderboard',dict(mode=1))['entries']],['Gamma'])
  self.app.handle('/profile/update',dict(name='Beta Max'),tb)
  self.assertEqual(self.app.handle('/leaderboard',dict(mode=0))['entries'][0]['name'],'Beta Max') # current nickname
  for data in (dict(mode=3),dict(),dict(mode=True),dict(mode='0'),dict(mode=0,today='yes')):self.assertEqual(self.status('/leaderboard',data),400,data)
  for i in range(55):self.record(self.member(f'm{i}')[0],1000+i)
  board=self.app.handle('/leaderboard',dict(mode=0),ta)
  self.assertEqual(len(board['entries']),50);self.assertEqual(board['entries'][0]['score'],1054);self.assertEqual(board['me']['rank'],57)
 def test_today_uses_kst_midnight(self):
  utc=lambda *parts:int(datetime(*parts,tzinfo=timezone.utc).timestamp())
  self.assertEqual(kst_midnight(utc(2026,10,27,15,30)),utc(2026,10,27,15,0)) # 00:30 KST on the 28th
  self.assertEqual(kst_midnight(utc(2026,10,27,14,59)),utc(2026,10,26,15,0)) # 23:59 KST on the 27th
  midnight=kst_midnight(time.time());a,ta=self.member('Early');b,_=self.member('Late')
  self.record(a,500,created=midnight-1,kcal=2);self.record(b,200,created=midnight,kcal=3);self.record(a,100,created=midnight,kcal=4)
  self.assertEqual([(e['name'],e['score']) for e in self.app.handle('/leaderboard',dict(mode=0,today=True))['entries']],[('Late',200),('Early',100)])
  self.assertEqual([e['name'] for e in self.app.handle('/leaderboard',dict(mode=0,today=False))['entries']],['Early','Late'])
  stats=self.app.handle('/stats',{},ta);self.assertEqual((stats['today_kcal'],stats['total_kcal']),(4.0,6.0))
 def test_stats(self):
  pid,token=self.member('Stats')
  self.assertEqual(self.status('/stats',{}),401)
  self.assertEqual(self.app.handle('/stats',{},token),dict(ok=True,today_kcal=0.0,total_kcal=0.0,best_score=[0,0,0]))
  self.record(pid,300,mode=0);self.record(pid,700,mode=0);self.record(pid,900,mode=0,version=1);self.record(pid,40,mode=2,kcal=0)
  self.app.handle('/runs',{k:v for k,v in run(score=5000).items() if k!='mode'},token) # no mode: counts kcal, never ranks
  self.assertEqual(self.app.handle('/stats',{},token),dict(ok=True,today_kcal=4.0,total_kcal=4.0,best_score=[700,0,40]))
 def test_no_face_and_malformed_images_rejected(self):
  _,image=self.cv.imencode('.jpg',self.np.zeros((480,640,3),self.np.uint8))
  blank=base64.b64encode(image).decode()
  with self.assertRaises(ApiError):self.faces.samples(dict(images=[blank]*3))
  with self.assertRaises(ApiError):self.faces.samples(dict(images=['not jpeg']*3))
  with self.assertRaises(ApiError):self.faces.samples(dict(images=[]))
 def test_non_jpeg_and_oversized_images_rejected(self):
  _,png=self.cv.imencode('.png',self.image)
  with self.assertRaisesRegex(ApiError,'JPEG'):self.faces.extract(base64.b64encode(png).decode())
  _,huge=self.cv.imencode('.jpg',self.np.zeros((2400,2400,3),self.np.uint8))
  with self.assertRaises(ApiError):self.faces.extract(base64.b64encode(huge).decode())
  try:decoded=self.cv.imdecode(huge,self.cv.IMREAD_COLOR)
  except self.cv.error:decoded=None
  self.assertIsNone(decoded) # refused from the header by OPENCV_IO_MAX_IMAGE_PIXELS, before allocating pixels
 def test_fresh_db_is_v2_and_newer_schema_refused(self):
  self.assertEqual(self.store.version(),2);self.assertTrue({'profiles_name_key','runs_mode_score'}<=self.store.indexes())
  self.assertEqual(migrate_account.migrate(database_url=self.url),[])
  self.store.db.close();self.store=self.make_store() # a restart keeps the single version row
  self.assertEqual(self.store.db.execute('SELECT version FROM schema_version').fetchall(),[(SCHEMA_VERSION,)])
  with self.store.db:self.store.db.execute('UPDATE schema_version SET version=3')
  self.store.db.close()
  with self.assertRaisesRegex(SchemaError,'newer'):self.store=self.make_store()
 def test_v1_db_refused_until_migrated(self):
  features,_=self.faces.samples(dict(images=self.images))
  with self.store.db:
   for table in ('runs','sessions','face_samples','profiles','schema_version'):self.store.db.execute('DROP TABLE '+table)
   self.store.db.execute(V1)
  # Nicknames were free text: case/space duplicates, an existing "kim2", a created_at tie and a long name.
  people=[('legacy','Legacy explorer',0),('c','Kim',1),('b','kim',2),('a','kim2',3),('e','Twin',5),('d','T win',5),('f','  ',6)]
  with self.store.db:
   self.store.db.executemany('INSERT INTO profiles VALUES(?,?,?,?,?)',[(pid,name,b'old-photo',created,'local-exhibition-v1') for pid,name,created in people])
   self.store.db.executemany('INSERT INTO face_samples VALUES(?,?)',[('legacy',f.tobytes()) for f in features])
   self.store.db.execute('INSERT INTO runs VALUES(?,?,?,?,?,?,?,?)',(str(uuid.uuid4()),'legacy',10,5,1,50,'[1,0,0,0,0]',1))
  self.store.db.close()
  with self.assertRaisesRegex(SchemaError,'migrate_account'):self.store=self.make_store()
  def fail(store):raise RuntimeError('step failed')
  with mock.patch.object(migrate_account,'STEPS',migrate_account.STEPS+(('fail',fail),)),self.assertRaises(RuntimeError):
   migrate_account.migrate(database_url=self.url)
  with self.assertRaisesRegex(SchemaError,'v1'):self.store=self.make_store() # every DDL step was rolled back
  self.assertEqual(migrate_account.migrate(database_url=self.url),[name for name,_ in migrate_account.STEPS])
  self.assertEqual(migrate_account.migrate(database_url=self.url),[])
  self.store=self.make_store();self.app=App(self.store,self.faces)
  self.assertEqual(self.store.version(),2);self.assertNotIn('avatar',self.store.columns('profiles'))
  names=dict(self.store.db.execute('SELECT id,name FROM profiles').fetchall())
  self.assertEqual(names,dict(legacy='Legacy exp',c='Kim',b='kim3',a='kim2',d='T win',e='Twin2',f='탐험가'))
  self.assertEqual(dict(self.store.db.execute('SELECT id,name_key FROM profiles').fetchall())['e'],'twin2')
  login=self.app.handle('/login',dict(images=self.images))
  self.assertEqual(login['profile'],dict(id='legacy',name='Legacy exp',weight=65.0,style=0,goal=50.0,coins=0,unlocked=[],tutorial_done=False))
  self.assertTrue(login['avatar'])
  self.assertEqual(len(self.app.handle('/history',{},login['token'])['runs']),1)
  self.assertEqual(self.app.handle('/stats',{},login['token'])['best_score'],[0,0,0]) # old records keep score_version 1
  self.assertEqual(self.status('/profile/update',dict(name='KIM'),login['token']),409)
  self.assertEqual(self.app.handle('/runs',run(coins=4),login['token'])['coins'],4)
 def test_invalid_and_unauthorized_records_rejected(self):
  _,token=self.member('Records')
  with self.assertRaises(ApiError):self.app.handle('/runs',{},token)
  with self.assertRaises(ApiError):self.app.handle('/delete',{},'bad')
class StorageTests(PostgresCase):
 def setUp(self):super().setUp();self.store=self.make_store();self.temp=tempfile.TemporaryDirectory();self.addCleanup(self.temp.cleanup)
 def tearDown(self):self.store.db.close()
 def profile(self):return ('visitor',"Test ' v",'test\'v',1,'test-consent')
 def test_transactions_rollback_and_leave_connection_open(self):
  with self.assertRaises(self.psycopg.errors.UniqueViolation):
   with self.store.db:self.store.db.execute(PROFILE_INSERT,self.profile());self.store.db.execute(PROFILE_INSERT,self.profile())
  self.assertEqual(self.store.db.execute('SELECT COUNT(*) FROM profiles').fetchone()[0],0)
  with self.store.db:self.store.db.execute(PROFILE_INSERT,self.profile())
  self.assertEqual(self.store.db.execute('SELECT name FROM profiles').fetchone()[0],self.profile()[1])
 def test_application_role_has_no_administrator_privileges(self):
  self.assertEqual(self.store.db.execute('SELECT rolsuper,rolcreatedb,rolcreaterole FROM pg_roles WHERE rolname=current_user').fetchone(),(False,False,False))
  with self.assertRaises(self.psycopg.errors.InsufficientPrivilege):self.store.db.execute('CREATE ROLE forbidden_test_role')
 def test_jsonb_binary_types_and_foreign_key_indexes(self):
  types=lambda table:dict(self.store.db.execute('SELECT column_name,data_type FROM information_schema.columns WHERE table_schema=current_schema() AND table_name=?',(table,)).fetchall())
  self.assertEqual(types('runs')['activities'],'jsonb')
  profiles=types('profiles');self.assertEqual((profiles['unlocked'],profiles['tutorial_done'],profiles['weight']),('jsonb','boolean','double precision'))
  self.assertTrue({'runs_profile_created','runs_mode_score','profiles_name_key','sessions_expiry','sessions_profile','face_samples_pkey'}<=self.store.indexes())
 def test_closed_transport_reconnects(self):
  self.store.db.raw.close();self.assertEqual(self.store.db.execute('SELECT 1').fetchone(),(1,))
 def test_docker_environment_uses_application_password_file(self):
  fields=self.psycopg.conninfo.conninfo_to_dict(URL);secret=Path(self.temp.name)/'app-password'
  secret.write_text(fields['password'],encoding='utf-8')
  env={'PGHOST':fields['host'],'PGPORT':str(fields['port']),'PGDATABASE':fields['dbname'],'PGUSER':fields['user'],
       'PGPASSWORD_FILE':str(secret),'PGOPTIONS':'-c search_path='+self.schema}
  with mock.patch.dict(os.environ,env):other=Store() # same PG* settings as compose.yaml
  try:self.assertEqual(other.db.execute('SELECT current_user,current_schema()').fetchone(),(fields['user'],self.schema))
  finally:other.db.close()
class ConfigTests(unittest.TestCase):
 def test_store_requires_postgres_settings(self):
  with mock.patch.dict(os.environ,{}),self.assertRaisesRegex(ConfigError,'IDENTITY_DATABASE_URL'):
   for name in ('PGHOST','PGSERVICE'):os.environ.pop(name,None)
   Store()
 def test_secret_generation_preserves_existing_values(self):
  with tempfile.TemporaryDirectory() as temp:
   folder=Path(temp)/'secrets';prepare(folder);before={file.name:file.read_bytes() for file in folder.iterdir()}
   prepare(folder);self.assertEqual(before,{file.name:file.read_bytes() for file in folder.iterdir()})
   self.assertNotEqual(before['db_app_password'],before['db_admin_password'])
if __name__=='__main__':unittest.main(verbosity=2)
