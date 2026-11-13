"""하네스 비용 측정 — 이 PC 의 Claude Code 세션 기록(jsonl)에서 지표를 뽑는다. 읽기만 한다.

사용:
    python -I tools/harness-cost.py                       # 전체 기간
    python -I tools/harness-cost.py --since 2026-11-13    # 그 날짜 이후 시작한 세션만
    python -I tools/harness-cost.py --root <기록 폴더> --json <출력.json>

기록 폴더는 실행할 때 구한다 (CLAUDE.md §4-3): ~/.claude/projects/<리포 절대 경로의 : \\ / 를 - 로 바꾼 이름>.
다른 PC 의 세션은 그 PC 에서만 잴 수 있다.

나오는 것 (docs/DECISIONS.md 2026-11-13 "하네스 비용 기준선" 의 표와 같은 항목):
  · 토큰: 메인·서브에이전트의 입력(캐시 읽기·쓰기 포함)·출력. 같은 API 응답이 여러 줄로 남으므로 message.id 로 한 번만 센다.
  · 요청당 컨텍스트 크기: 중앙값, 크기 구간별 요청 수와 입력 몫, 세션 첫 요청(고정 비용).
  · 조회: 인덱스(symbols.tsv) 조회 대 코드 검색(Grep 도구 + 셸 grep/rg) 대 .cs Read. 셸 명령 분류는 문자열 근사다.
  · 파견: 메인이 부른 Agent 의 subagent_type 별 횟수.
  · 권한 결정(훅 차단은 reject/hook), 사용자 거절 문구, 도구 오류.
안 나오는 것: 달러 비용(토큰 종류별 단가를 반영하지 않는다), Codex 사용량(리뷰는 사용량을 남기지 않는다).
"""
import argparse, collections, glob, json, os, re, statistics, sys
from datetime import datetime, timezone

sys.stdout.reconfigure(encoding='utf-8')
sys.stderr.reconfigure(encoding='utf-8')

BANDS = [(1e5, '<100k'), (2e5, '100-200k'), (4e5, '200-400k'), (7e5, '400-700k'), (float('inf'), '>=700k')]
# 셸 명령을 && ; || 줄바꿈 | 로 나눈 조각 가운데, 파이프 뒤가 아닌(앞 명령 출력을 거르지 않는) 검색만 센다.
SEGMENT = re.compile(r'(&&|\|\||;|\n|\|)')
SEARCH_HEAD = re.compile(r'^\s*(?:cd\s+\S+\s*)?(grep|rg|git\s+grep|select-string)\b', re.I)
INDEX_SHELL = re.compile(r'(grep|rg|select-string|head|tail|cat|sed|awk|get-content)[^|;&\n]*symbols\.tsv', re.I)


def file_searches(cmd):
    """파일을 직접 찾는 grep/rg 조각 수 (`git log | grep x` 같은 출력 거르기는 빼고)."""
    parts = SEGMENT.split(cmd)
    n, prev = 0, None
    for p in parts:
        if SEGMENT.fullmatch(p):
            prev = p
            continue
        if prev != '|' and SEARCH_HEAD.match(p):
            n += 1
        prev = None
    return n


def default_root():
    repo = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    return os.path.join(os.path.expanduser('~'), '.claude', 'projects', re.sub(r'[:\\/]', '-', repo))


def band(c):
    return next(name for limit, name in BANDS if c < limit)


def text_of(content):
    if isinstance(content, str):
        return content
    if isinstance(content, list):
        out = []
        for b in content:
            if isinstance(b, dict) and b.get('type') == 'text':
                out.append(b.get('text', ''))
            elif isinstance(b, dict) and b.get('type') == 'tool_result':
                out.append(text_of(b.get('content')))
        return '\n'.join(out)
    return ''


def read_kind(path):
    p = (path or '').replace('\\', '/').lower()
    if p.endswith('index/symbols.tsv'): return 'index'
    if p.endswith('.cs'): return 'code:.cs'
    if '/docs/' in p or p.endswith('claude.md'): return 'docs'
    if p.endswith(('.unity', '.prefab', '.asset', '.mat', '.inputactions')): return 'yaml-asset'
    if p.endswith(('.png', '.jpg', '.jpeg')): return 'image'
    return 'other'


class Stats:
    def __init__(self):
        self.tok = collections.Counter()
        self.requests = 0
        self.ctx = []
        self.tools = collections.Counter()
        self.lookup = collections.Counter()
        self.reads = collections.Counter()
        self.agents = collections.Counter()
        self.perm = collections.Counter()
        self.tool_err = collections.Counter()
        self.rejected = 0
        self.bad_lines = 0


def first_timestamp(path):
    """세션의 첫 timestamp(UTC). 없으면 None (빈 세션)."""
    with open(path, encoding='utf-8') as f:
        for line in f:
            m = re.search(r'"timestamp":"([^"]+)"', line)
            if m:
                return datetime.fromisoformat(m.group(1).replace('Z', '+00:00'))
    return None


def scan(path, st, seen):
    """한 jsonl 을 읽어 st 에 더한다."""
    id2tool = {}
    with open(path, encoding='utf-8') as f:
        for line in f:
            try:
                d = json.loads(line)
            except json.JSONDecodeError:
                st.bad_lines += 1
                continue
            t = d.get('type')
            m = d.get('message') or {}
            if t == 'assistant':
                mid = m.get('id') or d.get('requestId')
                if mid and mid not in seen and m.get('model') != '<synthetic>':
                    seen.add(mid)
                    u = m.get('usage') or {}
                    st.requests += 1
                    for k in ('input_tokens', 'cache_creation_input_tokens', 'cache_read_input_tokens', 'output_tokens'):
                        st.tok[k] += u.get(k) or 0
                    ctx = sum(u.get(k) or 0 for k in ('input_tokens', 'cache_creation_input_tokens', 'cache_read_input_tokens'))
                    if ctx:
                        st.ctx.append(ctx)
                for b in m.get('content') or []:
                    if not (isinstance(b, dict) and b.get('type') == 'tool_use'):
                        continue
                    name = b.get('name') or '?'
                    inp = b.get('input') or {}
                    short = 'mcp:' + name.split('__')[-1] if name.startswith('mcp__') else name
                    st.tools[short] += 1
                    id2tool[b.get('id')] = short
                    if name == 'Read':
                        k = read_kind(inp.get('file_path'))
                        st.reads[k] += 1
                        if k == 'index': st.lookup['index'] += 1
                    elif name == 'Grep':
                        where = f"{inp.get('path', '')} {inp.get('glob', '')}"
                        st.lookup['index' if 'symbols.tsv' in where else 'code:Grep'] += 1
                    elif name in ('Bash', 'PowerShell'):
                        cmd = str(inp.get('command', ''))
                        if INDEX_SHELL.search(cmd): st.lookup['index'] += 1
                        elif file_searches(cmd): st.lookup['code:shell grep'] += 1  # 명령 단위 (Grep 도구 1회와 같은 단위)
                    elif name in ('Agent', 'Task'):
                        st.agents[inp.get('subagent_type') or 'general-purpose'] += 1
            elif t == 'user':
                pdx = d.get('permissionDecision')
                if isinstance(pdx, dict):
                    st.perm[f"{pdx.get('decision')}/{pdx.get('source')}"] += 1
                c = m.get('content')
                if isinstance(c, list):
                    for b in c:
                        if isinstance(b, dict) and b.get('type') == 'tool_result':
                            txt = text_of(b.get('content'))
                            if "doesn't want to proceed" in txt:
                                st.rejected += 1
                            elif b.get('is_error'):
                                st.tool_err[id2tool.get(b.get('tool_use_id'), '?')] += 1


def main():
    ap = argparse.ArgumentParser(description=__doc__.split('\n')[0])
    ap.add_argument('--root', default=None, help='세션 기록 폴더 (기본: 이 리포의 ~/.claude/projects 폴더)')
    ap.add_argument('--since', default=None, help='YYYY-MM-DD — 그 날짜(UTC) 이후 시작한 세션만')
    ap.add_argument('--json', default=None, help='세션별 원자료를 이 경로에 쓴다')
    a = ap.parse_args()

    root = a.root or default_root()
    if not os.path.isdir(root):
        raise SystemExit(f'harness-cost: 세션 기록 폴더가 없다: {root} (--root 로 지정한다)')
    since = datetime.fromisoformat(a.since).replace(tzinfo=timezone.utc) if a.since else None

    main_st, sub_st = Stats(), Stats()
    first_ctx, sub_first_ctx, per_session = [], [], []
    sub_kind = collections.Counter()
    skipped = 0
    for f in sorted(glob.glob(os.path.join(root, '*.jsonl'))):
        sid = os.path.basename(f)[:-6]
        start = first_timestamp(f)
        if start is None or (since and start < since):
            skipped += 1
            continue
        n0 = len(main_st.ctx)
        scan(f, main_st, set())
        if len(main_st.ctx) > n0:
            first_ctx.append(main_st.ctx[n0])
        subs = sorted(glob.glob(os.path.join(root, sid, 'subagents', '*.jsonl')))
        sub_in = 0
        for sf in subs:
            meta = sf[:-6] + '.meta.json'
            kind = 'unknown'
            if os.path.exists(meta):
                with open(meta, encoding='utf-8') as mf:
                    kind = json.load(mf).get('agentType') or 'unknown'
            sub_kind[kind] += 1
            before_in = sum(sub_st.tok[k] for k in ('input_tokens', 'cache_creation_input_tokens', 'cache_read_input_tokens'))
            n1 = len(sub_st.ctx)
            scan(sf, sub_st, set())
            if len(sub_st.ctx) > n1:
                sub_first_ctx.append(sub_st.ctx[n1])
            sub_in += sum(sub_st.tok[k] for k in ('input_tokens', 'cache_creation_input_tokens', 'cache_read_input_tokens')) - before_in
        per_session.append(dict(session=sid, start=start.isoformat(), requests=len(main_st.ctx) - n0,
                                subagents=len(subs), subagent_input=sub_in))

    if not per_session:
        raise SystemExit(f'harness-cost: 조건에 맞는 세션이 없다 (root={root}, since={a.since})')

    def tin(st):
        return sum(st.tok[k] for k in ('input_tokens', 'cache_creation_input_tokens', 'cache_read_input_tokens'))

    allc = main_st.ctx
    total_in = tin(main_st) + tin(sub_st)
    print(f'세션 {len(per_session)}개 (건너뜀 {skipped}), 서브에이전트 기록 {sum(sub_kind.values())}개, 기간 시작 {a.since or "전체"}')
    print(f'읽지 못한 줄: 메인 {main_st.bad_lines}, 서브 {sub_st.bad_lines}')
    print('\n[토큰]')
    print(f'  메인 요청 {main_st.requests}  입력 {tin(main_st):,} (캐시 읽기 {main_st.tok["cache_read_input_tokens"] * 100 / max(tin(main_st), 1):.1f}%)  출력 {main_st.tok["output_tokens"]:,}')
    print(f'  서브 요청 {sub_st.requests}  입력 {tin(sub_st):,} (전체 입력의 {tin(sub_st) * 100 / max(total_in, 1):.1f}%)  출력 {sub_st.tok["output_tokens"]:,}')
    print('\n[요청당 컨텍스트 — 메인]')
    print(f'  중앙값 {int(statistics.median(allc)):,}  평균 {int(statistics.mean(allc)):,}  최대 {max(allc):,}')
    print(f'  세션 첫 요청 중앙값 {int(statistics.median(first_ctx)):,} (서브 {int(statistics.median(sub_first_ctx)) if sub_first_ctx else 0:,})')
    bn, bt = collections.Counter(), collections.Counter()
    for c in allc:
        bn[band(c)] += 1
        bt[band(c)] += c
    for _, name in BANDS:
        print(f'  {name:9} 요청 {bn[name]:5} ({bn[name] * 100 / len(allc):4.1f}%)  입력 몫 {bt[name] * 100 / sum(allc):4.1f}%')
    print('\n[조회 — 메인 + 서브]')
    lk = main_st.lookup + sub_st.lookup
    rd = main_st.reads + sub_st.reads
    print(f'  인덱스 {lk["index"]}  코드 Grep 도구 {lk["code:Grep"]}  셸 grep/rg {lk["code:shell grep"]}  .cs Read {rd["code:.cs"]}')
    print(f'  Read 분류 {dict(rd.most_common())}')
    print('\n[파견 — 메인의 Agent 호출]')
    print(f'  {dict(main_st.agents.most_common())}')
    print(f'  서브에이전트 기록 종류 {dict(sub_kind.most_common())}')
    print('\n[권한·거절·오류 — 메인 + 서브]')
    perm = main_st.perm + sub_st.perm
    print(f'  권한 결정 {dict(perm.most_common())}')
    print(f'  훅 차단(reject/hook) {perm["reject/hook"]}  사용자 거절 문구 {main_st.rejected + sub_st.rejected}')
    errs = main_st.tool_err + sub_st.tool_err
    print(f'  도구 오류 {dict(errs.most_common(8))}')
    print(f'  Unity_RunCommand 호출 {(main_st.tools + sub_st.tools)["mcp:Unity_RunCommand"]}, 오류 {errs["mcp:Unity_RunCommand"]}')

    if a.json:
        with open(a.json, 'w', encoding='utf-8') as out:
            json.dump(per_session, out, ensure_ascii=False, indent=1)
        print(f'\n세션별 원자료: {a.json}')


if __name__ == '__main__':
    main()
