"""Mongscape exhibition profiles. YuNet + SFace, PostgreSQL, Docker/HTTPS host.
This demo has no certified presentation-attack/liveness detection. Do not use for sensitive accounts.
"""
from __future__ import annotations
import argparse, base64, hashlib, json, math, os, sys, time, uuid
from pathlib import Path
from http.server import BaseHTTPRequestHandler, HTTPServer
BASE = Path(__file__).resolve().parent
sys.path.insert(0, str(BASE / '.runtime'))
MAX_BODY = 4 * 1024 * 1024
MAX_SIDE = 1920
# OpenCV refuses larger images from the header, before allocating pixels: a tiny JPEG can declare
# 14000x14000 and decode to 0.6 GB. Read on the first decode, so setting it at import is enough.
os.environ.setdefault('OPENCV_IO_MAX_IMAGE_PIXELS', str(MAX_SIDE * MAX_SIDE))
from storage import NAME_MAX,ApiError,ConfigError,SchemaError,Store,clean_name,name_key
# Attempts per minute. Each client IP has its own budget, so one sender cannot lock out every PC;
# the server-wide caps bound face inference CPU. PCs behind one NAT share their IP budget.
LIMIT_WINDOW=60; FACE_LIMIT=15; FACE_LIMIT_ALL=60; NAME_CHECK_LIMIT=60; NAME_CHECK_LIMIT_ALL=240
LEADERBOARD_LIMIT=120; RUNS_LIMIT=20 # per client IP / per account
ITEM_PRICES={0:30,1:60,2:100} # ItemKind: magnet, coin x2, shield; same as client ItemCatalog
MODES=range(3) # PlayStyle: 0 full body, 1 upper body, 2 keyboard
DEFAULT_WEIGHT=65; WEIGHT_MIN,WEIGHT_MAX=10,350; LEADERBOARD_SIZE=50; CONSENT_VERSION='account-v3' # weight range = the game's WorkoutEstimate
KST=9*3600
def kst_midnight(now): return (int(now)+KST)//86400*86400-KST
def number(data,key,low,high,integer=False,message=None):
    v=data.get(key) # bool is not accepted: type() is checked, not isinstance()
    if type(v) not in ((int,) if integer else (int,float)) or not math.isfinite(v) or v<low or v>high: raise ApiError(message or '잘못된 값: '+key)
    return v

class Faces:
    def __init__(self, models):
        import cv2, numpy as np
        self.cv, self.np=cv2,np
        cv2.setNumThreads(2)
        self.detector=cv2.FaceDetectorYN.create(str(models/'face_detection_yunet_2023mar.onnx'),'',(320,320),.9,.3,5000)
        self.recognizer=cv2.FaceRecognizerSF.create(str(models/'face_recognition_sface_2021dec.onnx'),'')
    def extract(self, encoded):
        try:
            raw=base64.b64decode(encoded,validate=True)
        except Exception: raise ApiError('카메라 이미지가 잘못되었습니다.')
        # The game sends JPEG only; refusing other formats also blocks PNG decompression bombs.
        if not raw.startswith(b'\xff\xd8\xff'): raise ApiError('카메라 이미지는 JPEG만 받습니다.')
        try: img=self.cv.imdecode(self.np.frombuffer(raw,self.np.uint8),self.cv.IMREAD_COLOR)
        except Exception: img=None # over OPENCV_IO_MAX_IMAGE_PIXELS or corrupt
        if img is None or max(img.shape[:2])>MAX_SIDE: raise ApiError('카메라 이미지 크기를 확인하세요.')
        self.detector.setInputSize((img.shape[1],img.shape[0])); _,faces=self.detector.detect(img)
        if faces is None or len(faces)!=1: raise ApiError('화면에 한 사람의 얼굴만 보이게 해 주세요.')
        f=faces[0]
        if min(f[2],f[3])<55: raise ApiError('얼굴을 카메라에 조금 더 가까이 해 주세요.')
        aligned=self.recognizer.alignCrop(img,f)
        feature=self.recognizer.feature(aligned).flatten().astype('float32')
        feature/=max(1e-9,float(self.np.linalg.norm(feature)))
        _,avatar=self.cv.imencode('.jpg',aligned,[self.cv.IMWRITE_JPEG_QUALITY,90])
        return feature,avatar.tobytes()
    def samples(self, data):
        images=data.get('images',[])
        if not isinstance(images,list) or len(images)!=3: raise ApiError('서로 다른 시점의 카메라 사진 3장이 필요합니다.')
        values=[self.extract(image) for image in images]
        if min(float(a[0]@b[0]) for a in values for b in values)<.6:
            raise ApiError('촬영 중 얼굴이 달라졌습니다. 혼자 서서 다시 시도하세요.')
        return [v[0] for v in values], values[0][1]
    def match(self, store, features):
        scores={}
        for pid,raw in store.db.execute('SELECT profile_id,embedding FROM face_samples'):
            known=self.np.frombuffer(raw,dtype='float32')
            # All three frames must match the same enrolled sample; use the weakest frame.
            score=min(float(known@candidate) for candidate in features)
            scores[pid]=max(scores.get(pid,-1),score)
        ranked=sorted(scores.items(),key=lambda pair:pair[1],reverse=True)
        if not ranked or ranked[0][1]<.6: return None
        if len(ranked)>1 and ranked[0][1]-ranked[1][1]<.08: raise ApiError('얼굴 구분이 불확실합니다. 밝은 곳에서 다시 시도하세요.',409)
        return ranked[0][0]

class App:
    def __init__(self, store, faces): self.store,self.faces=store,faces; self.attempts={}
    def limit(self,key,maximum):
        now=time.monotonic()
        if len(self.attempts)>4096: # keys include client IPs; forget the idle ones
            self.attempts={k:v for k,v in self.attempts.items() if v and now-v[-1]<LIMIT_WINDOW}
        recent=[t for t in self.attempts.get(key,[]) if now-t<LIMIT_WINDOW]
        if len(recent)>=maximum: self.attempts[key]=recent; raise ApiError('잠시 기다린 후 다시 시도하세요.',429)
        self.attempts[key]=recent+[now]
    def nickname(self,value,owner=None):
        name=clean_name(value)
        if not name: raise ApiError(f'닉네임은 1~{NAME_MAX}자로 입력하세요. 제어 문자는 쓸 수 없습니다.')
        if self.store.name_owner(name_key(name)) not in (None,owner): raise ApiError('이미 사용 중인 닉네임입니다.',409)
        return name
    def settings(self,data):
        """Profile fields present in the request; weight null means the 65 kg default."""
        values={}
        if 'weight' in data: values['weight']=DEFAULT_WEIGHT if data['weight'] is None else number(data,'weight',WEIGHT_MIN,WEIGHT_MAX,message=f'몸무게는 {WEIGHT_MIN}~{WEIGHT_MAX}kg 숫자로 입력하세요.')
        if 'style' in data: values['style']=number(data,'style',0,2,True)
        if 'goal' in data: values['goal']=number(data,'goal',1,5000)
        if 'tutorial_done' in data:
            if type(data['tutorial_done']) is not bool: raise ApiError('잘못된 값: tutorial_done')
            values['tutorial_done']=data['tutorial_done']
        return values
    def handle(self,path,data,token='',client=''):
        if path=='/name-check':
            self.limit(('name-check',client),NAME_CHECK_LIMIT); self.limit(('name-check',),NAME_CHECK_LIMIT_ALL)
            try: self.nickname(data.get('name'))
            except ApiError as e: return dict(ok=True,available=False,message=e.message)
            return dict(ok=True,available=True,message='사용할 수 있는 닉네임입니다.')
        if path in ('/enroll','/login'):
            self.limit(('face',client),FACE_LIMIT); self.limit(('face',),FACE_LIMIT_ALL)
            if path=='/enroll':
                if data.get('consent') is not True: raise ApiError('얼굴 저장 동의가 필요합니다.')
                name=self.nickname(data.get('name')); settings=self.settings(data); settings.pop('tutorial_done',None)
            features,avatar=self.faces.samples(data); pid=self.faces.match(self.store,features)
            if path=='/enroll':
                if pid: raise ApiError('이미 등록된 얼굴입니다. 얼굴 로그인을 눌러 주세요.',409)
                pid=uuid.uuid4().hex
                values=dict(id=pid,name=name,name_key=name_key(name),created_at=int(time.time()),consent_version=CONSENT_VERSION,**settings)
                with self.store.db:
                    # Column names come from the fixed keys above; values stay parameters.
                    self.store.db.execute(f'INSERT INTO profiles ({",".join(values)}) VALUES({",".join("?"*len(values))})',tuple(values.values()))
                    self.store.db.executemany('INSERT INTO face_samples VALUES(?,?) ON CONFLICT DO NOTHING',[(pid,f.tobytes()) for f in features])
            if not pid: raise ApiError('등록된 얼굴을 찾지 못했습니다. 먼저 얼굴을 등록하세요.',401)
            # The face crop only travels back in this response; the DB keeps features, never photos.
            session=self.store.issue(pid); return self.store.profile(pid,session,avatar)
        if path=='/leaderboard':
            self.limit(('leaderboard',client),LEADERBOARD_LIMIT); return self.leaderboard(data,token)
        pid=self.store.authorize(token)
        if path=='/profile': return dict(ok=True,profile=self.store.account(pid))
        if path=='/profile/update':
            values=self.settings(data)
            if 'name' in data: values['name']=self.nickname(data['name'],pid); values['name_key']=name_key(values['name'])
            if values:
                with self.store.db: self.store.db.execute('UPDATE profiles SET '+','.join(k+'=?' for k in values)+' WHERE id=?',(*values.values(),pid))
            return dict(ok=True,profile=self.store.account(pid))
        if path=='/delete': self.store.delete(pid); return dict(ok=True)
        if path=='/logout':
            with self.store.db: self.store.db.execute('DELETE FROM sessions WHERE token_hash=?',(hashlib.sha256(token.encode()).hexdigest(),))
            return dict(ok=True)
        if path=='/runs':
            self.limit(('runs',pid),RUNS_LIMIT)
            distance=number(data,'distance',0,10000000); score=number(data,'score',0,2147483647); kcal=number(data,'kcal',0,100000); goal=number(data,'goal',1,5000)
            activities=data.get('activities',[])
            if not isinstance(activities,list) or len(activities)!=5 or any(type(v) not in (float,int) or not math.isfinite(v) or v<0 for v in activities): raise ApiError('동작별 기록 오류')
            # Clients built before accounts send no mode; their records are kept unranked (mode NULL, version 1).
            mode=number(data,'mode',0,2,True) if 'mode' in data else None
            coins=number(data,'coins',0,5000,True) if 'coins' in data else 0
            play_seconds=number(data,'play_seconds',0,86400) if 'play_seconds' in data else 0
            version=number(data,'score_version',1,2,True) if 'score_version' in data else 1
            try: rid=str(uuid.UUID(str(data.get('id',''))))
            except ValueError: raise ApiError('기록 ID 오류')
            with self.store.db:
                added=self.store.db.execute('INSERT INTO runs (id,profile_id,distance,score,kcal,goal,activities,created_at,mode,coins,score_version,play_seconds) '
                    'VALUES(?,?,?,?,?,?,?,?,?,?,?,?) ON CONFLICT DO NOTHING',
                    (rid,pid,distance,int(score),kcal,goal,json.dumps(activities),int(time.time()),mode,coins,version,play_seconds)).rowcount
                # A resent record id (retry after a lost reply) is stored once and credits coins once.
                if added and coins: self.store.db.execute('UPDATE profiles SET coins=coins+? WHERE id=?',(coins,pid))
                balance=self.store.db.execute('SELECT coins FROM profiles WHERE id=?',(pid,)).fetchone()[0]
            return dict(ok=True,coins=balance)
        if path=='/shop/buy':
            item=data.get('item')
            if type(item) is not int or item not in ITEM_PRICES: raise ApiError('없는 아이템입니다.')
            with self.store.db:
                profile=self.store.account(pid)
                if item in profile['unlocked']: raise ApiError('이미 해금한 아이템입니다.',409)
                if profile['coins']<ITEM_PRICES[item]: raise ApiError('코인이 부족합니다.')
                self.store.db.execute('UPDATE profiles SET coins=coins-?,unlocked=? WHERE id=? AND coins>=?',
                    (ITEM_PRICES[item],json.dumps(sorted(profile['unlocked']+[item])),pid,ITEM_PRICES[item]))
            return dict(ok=True,profile=self.store.account(pid))
        if path=='/stats':
            today,total=self.store.db.execute('SELECT COALESCE(SUM(CASE WHEN created_at>=? THEN kcal ELSE 0 END),0),COALESCE(SUM(kcal),0) FROM runs WHERE profile_id=?',
                (kst_midnight(time.time()),pid)).fetchone()
            best=dict(self.store.db.execute('SELECT mode,MAX(score) FROM runs WHERE profile_id=? AND score_version=2 AND mode IS NOT NULL GROUP BY mode',(pid,)).fetchall())
            return dict(ok=True,today_kcal=float(today),total_kcal=float(total),best_score=[int(best.get(m,0)) for m in MODES])
        if path=='/history':
            rows=self.store.db.execute('SELECT distance,score,kcal,goal,created_at FROM runs WHERE profile_id=? ORDER BY created_at DESC LIMIT 20',(pid,)).fetchall()
            return dict(ok=True,runs=[dict(zip(('distance','score','kcal','goal','created_at'),row)) for row in rows])
        raise ApiError('경로 없음',404)
    def leaderboard(self,data,token):
        mode=data.get('mode'); today=data.get('today',False)
        if type(mode) is not int or mode not in MODES: raise ApiError('잘못된 모드')
        if type(today) is not bool: raise ApiError('잘못된 값: today')
        # Best record per member (score, then the earlier record); only the current score formula ranks.
        rows=self.store.db.execute('SELECT p.name,r.score,r.distance,r.created_at,r.profile_id FROM ('
            'SELECT profile_id,score,distance,created_at,ROW_NUMBER() OVER (PARTITION BY profile_id ORDER BY score DESC,created_at,id) AS n '
            'FROM runs WHERE mode=? AND score_version=2 AND created_at>=?) r JOIN profiles p ON p.id=r.profile_id '
            'WHERE r.n=1 ORDER BY r.score DESC,r.created_at,r.profile_id',(mode,kst_midnight(time.time()) if today else 0)).fetchall()
        result=dict(ok=True,entries=[dict(rank=i+1,name=r[0],score=r[1],distance=r[2],created_at=r[3]) for i,r in enumerate(rows[:LEADERBOARD_SIZE])])
        try: pid=self.store.authorize(token) if token else None
        except ApiError: pid=None # the board is public; an expired token only hides "me"
        me=next((dict(rank=i+1,score=r[1],distance=r[2]) for i,r in enumerate(rows) if r[4]==pid),None)
        if me: result['me']=me
        return result

class Handler(BaseHTTPRequestHandler):
    def setup(self):
        super().setup()
        self.connection.settimeout(15)
    def client(self):
        # Caddy drops any X-Forwarded-For a client sends and puts the address it saw; the last entry is
        # that address. Only Caddy and the host loopback can reach the API, so the header is not spoofable.
        forwarded=self.headers.get('X-Forwarded-For','')
        return forwarded.rsplit(',',1)[-1].strip() if forwarded else self.client_address[0]
    def log_message(self,*args): pass # Images, tokens and identities never go into request logs.
    def answer(self,status,payload):
        body=json.dumps(payload,ensure_ascii=False).encode()
        self.send_response(status); self.send_header('Content-Type','application/json; charset=utf-8')
        self.send_header('Cache-Control','no-store'); self.send_header('Content-Length',str(len(body))); self.end_headers(); self.wfile.write(body)
    def do_GET(self):
        if self.path=='/health':
            try:
                self.server.app.store.db.execute('SELECT 1').fetchone()
                self.answer(200,dict(ok=True,service='Mongscape Local Identity',liveness=False,storage=self.server.app.store.backend))
            except Exception:self.answer(503,dict(ok=False,message='Identity database unavailable'))
        else: self.answer(404,dict(ok=False,message='Not found'))
    def do_POST(self):
        try:
            if self.headers.get('Origin'): raise ApiError('브라우저 요청은 허용하지 않습니다.',403)
            if self.headers.get('Host','').lower() not in self.server.allowed_hosts: raise ApiError('호스트 오류',403)
            if self.headers.get('Content-Type','').split(';')[0]!='application/json': raise ApiError('JSON 필요',415)
            try: length=int(self.headers.get('Content-Length','0'))
            except ValueError: raise ApiError('길이 오류')
            if length<=0 or length>MAX_BODY: raise ApiError('요청 크기 오류',413)
            try: data=json.loads(self.rfile.read(length))
            except Exception: raise ApiError('JSON 오류')
            if not isinstance(data,dict): raise ApiError('JSON 객체 필요')
            token=self.headers.get('Authorization','').removeprefix('Bearer ')
            self.answer(200,self.server.app.handle(self.path,data,token,self.client()))
        except ApiError as e: self.answer(e.status,dict(ok=False,message=e.message))
        except Exception as e:
            print('Identity error:',type(e).__name__,file=sys.stderr,flush=True)
            self.answer(500,dict(ok=False,message='프로필 처리 오류. 서버 로그를 확인하세요.'))

def create_server(host,port,store,faces,allowed_hosts=None):
    hosts={value.strip().lower() for value in (allowed_hosts or f'127.0.0.1:{port},localhost:{port}').split(',') if value.strip()}
    if not hosts or any('*' in value or '/' in value for value in hosts):
        raise ValueError('Allowed hosts must be explicit host[:port] values.')
    server=HTTPServer((host,port),Handler)
    server.app=App(store,faces)
    server.allowed_hosts=hosts
    return server

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--database-url',default=os.getenv('IDENTITY_DATABASE_URL'),help='PostgreSQL URL; PG* environment variables are used when omitted')
    parser.add_argument('--models',type=Path,default=BASE/'models')
    parser.add_argument('--host',default=os.getenv('IDENTITY_HOST','127.0.0.1'))
    parser.add_argument('--port',type=int,default=int(os.getenv('IDENTITY_PORT','8765')))
    parser.add_argument('--allowed-hosts',default=os.getenv('IDENTITY_ALLOWED_HOSTS'))
    parser.add_argument('--init-db',action='store_true')
    args=parser.parse_args()
    try:store=Store(database_url=args.database_url)
    except (ConfigError,SchemaError) as error:raise SystemExit(str(error))
    except Exception as error:raise SystemExit('Identity database connection/setup failed: '+type(error).__name__)
    try:
        if args.init_db: print('Identity database initialized:',store.backend); return
        try: faces=Faces(args.models)
        except Exception as e: raise SystemExit('Prepare runtime/models with SetupIdentity.ps1 or Docker: '+str(e))
        with create_server(args.host,args.port,store,faces,args.allowed_hosts) as server:
            print(f'Mongscape identity ready at {args.host}:{args.port} (exhibition profiles; no liveness)',flush=True)
            server.serve_forever()
    finally: store.db.close()
if __name__=='__main__': main()
