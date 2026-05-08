#!/bin/bash
# ============================================================
#  gMKVExtractGUI macOS 端口·夜间自动调度
#
#  架构:
#    Opus 调度中枢 (claude -p) → 生成 briefing 文件
#    Worker1: claude-code   → src/gMKVToolNix.Core 业务库 + 跨平台修复
#    Worker2: claude-code-2 → src/gMKVExtractGUI.Avalonia UI 层
#
#  用法:
#    cd /Applications/gMKVExtractGUI
#    chmod +x night_runner_macos.sh
#    nohup ./night_runner_macos.sh &
#
#  停止:
#    touch /Applications/gMKVExtractGUI/.orchestration/STOP
#
#  日志:
#    tail -f /Applications/gMKVExtractGUI/.orchestration/logs/night.log
# ============================================================

set -uo pipefail

export PATH="$HOME/.local/bin:$HOME/.pyenv/shims:/opt/homebrew/bin:$PATH"

PROJECT_DIR="/Applications/gMKVExtractGUI"
ORCH_DIR="$PROJECT_DIR/.orchestration"
BRIEFING_DIR="$ORCH_DIR/briefings"
HANDOFF_DIR="$ORCH_DIR/handoffs"
LOG_DIR="$ORCH_DIR/logs"
STOP_FILE="$ORCH_DIR/STOP"

MAX_ROUNDS="${MAX_ROUNDS:-12}"
WORKER_TIMEOUT="${WORKER_TIMEOUT:-2400}"
DISPATCH_TIMEOUT="${DISPATCH_TIMEOUT:-300}"
SLEEP_BETWEEN="${SLEEP_BETWEEN:-60}"

RATE_LIMIT_WAIT="${RATE_LIMIT_WAIT:-1200}"
RATE_LIMIT_MAX_WAIT=7200
RATE_LIMIT_BACKOFF=2
_rl_consecutive=0

mkdir -p "$LOG_DIR" "$HANDOFF_DIR" "$BRIEFING_DIR"
rm -f "$STOP_FILE"
NIGHT_LOG="$LOG_DIR/night.log"

log() {
    echo "[$(date '+%m-%d %H:%M:%S')] $*" | tee -a "$NIGHT_LOG"
}

is_rate_limited() {
    local log_file="$1"
    [ -f "$log_file" ] || return 1
    grep -qiE "rate.?limit|429|too many request|quota.?exceed|usage.?limit|overloaded|claude is currently|capacity" \
        "$log_file" 2>/dev/null
}

do_rate_limit_wait() {
    _rl_consecutive=$(( _rl_consecutive + 1 ))
    local wait_sec=$(( RATE_LIMIT_WAIT * ( RATE_LIMIT_BACKOFF ** (_rl_consecutive - 1) ) ))
    if [ "$wait_sec" -gt "$RATE_LIMIT_MAX_WAIT" ]; then
        wait_sec=$RATE_LIMIT_MAX_WAIT
    fi
    local wait_min=$(( wait_sec / 60 ))
    log "⏳ 检测到 Claude 额度限制（第 ${_rl_consecutive} 次），等待 ${wait_min} 分钟后重试..."
    log "   可 touch $STOP_FILE 随时中止"

    local elapsed=0
    while [ "$elapsed" -lt "$wait_sec" ]; do
        sleep 60
        elapsed=$(( elapsed + 60 ))
        if [ -f "$STOP_FILE" ]; then
            log "🛑 等待期间检测到 STOP，退出"
            rm -f "$STOP_FILE"
            exit 0
        fi
        local remaining=$(( wait_sec - elapsed ))
        if [ $(( remaining % 300 )) -eq 0 ] && [ "$remaining" -gt 0 ]; then
            log "   还需等待约 $(( remaining / 60 )) 分钟..."
        fi
    done
    log "✅ 等待完毕，恢复执行"
}

run_dispatch() {
    local round="$1"
    local round_str
    round_str=$(printf '%03d' "$round")
    log "🧠 Opus 调度中枢: 规划 Round $round_str"

    local handoff_context=""
    for f in "$HANDOFF_DIR"/claude-code.md "$HANDOFF_DIR"/claude-code-2.md; do
        [ -f "$f" ] || continue
        handoff_context+="
=== $(basename "$f") ===
$(head -120 "$f")

"
    done

    if [ -z "$handoff_context" ]; then
        handoff_context="(首轮运行,暂无 handoff 记录。请阅读 PROGRESS.md 了解 Round 0 已完成的脚手架内容)"
    fi

    local dispatch_log="$LOG_DIR/dispatch_r${round_str}.log"
    local prompt_file="$LOG_DIR/_dispatch_prompt_r${round_str}.md"

    local plan_context=""
    if [ -f "$ORCH_DIR/PLAN.md" ]; then
        plan_context=$(cat "$ORCH_DIR/PLAN.md")
    fi

    local progress_context=""
    if [ -f "$ORCH_DIR/PROGRESS.md" ]; then
        progress_context=$(cat "$ORCH_DIR/PROGRESS.md")
    fi

    cat > "$prompt_file" << DISPATCH_PROMPT
你是 gMKVExtractGUI macOS 端口的调度中枢 (Opus)。当前是夜间自动模式,只有 claude-code 和 claude-code-2 两个 worker 可用。

## 项目目标
把 .NET 4.0 WinForms 项目移植到 macOS 原生（.NET 9 + Avalonia 11），保留全部业务逻辑，UI 用马卡龙糖果配色 + 真毛玻璃。Round 0 已经完成项目骨架（见 PROGRESS.md），现在按 PLAN.md 推进。

## 工作目录
所有 worker 的 cwd 都是 /Applications/gMKVExtractGUI

## 文件管辖
完整定义见下面 PLAN.md 的「文件管辖」节，**以 PLAN.md 为唯一真相来源**。
关键提醒：
- claude-code 负责业务库（src/gMKVToolNix.Core/ 和共享源 src/gMKVToolNix/**/*.cs）
- claude-code-2 负责 Avalonia UI（src/gMKVExtractGUI.Avalonia/）
- Round 1-11 期间所有「Windows 残留」都是硬禁区
- Round 12 是唯一可碰硬禁区的轮次，由调度中枢在 briefing 中显式放开

## PLAN.md（开发计划）
$plan_context

## PROGRESS.md（当前状态）
$progress_context

## 各 worker 上轮 handoff
$handoff_context

## 你的任务
1. 阅读 handoff、PLAN.md、PROGRESS.md，判断当前推进到哪一轮
2. 按 PLAN.md 为 claude-code 和 claude-code-2 各生成 briefing
3. briefing 必须包含：Round 号、背景上下文、文件管辖（重申硬约束）、Task 列表、验证步骤、完成后写 handoff 的命令
4. 任务粒度控制在 40 分钟内可完成
5. 若某 worker 本轮无任务，content 写 "本轮无任务"
6. 项目完成时两份都写 "项目已完成,无需继续"

## 关键路径约束
briefing 中所有涉及 .orchestration 的路径写成相对路径：
- .orchestration/handoffs/claude-code.md
- .orchestration/handoffs/claude-code-2.md
worker 的 cwd 是 /Applications/gMKVExtractGUI

## Build 验证命令（worker 必须自验证）
- 业务库: cd /Applications/gMKVExtractGUI && dotnet build src/gMKVToolNix.Core/gMKVToolNix.Core.csproj
- UI:     cd /Applications/gMKVExtractGUI && dotnet build src/gMKVExtractGUI.Avalonia/gMKVExtractGUI.Avalonia.csproj
- Run UI: cd /Applications/gMKVExtractGUI && dotnet run --project src/gMKVExtractGUI.Avalonia

## 输出格式（严格遵守，不要加 markdown 代码块）

===BRIEFING:claude-code===
(claude-code.md 完整内容)

===BRIEFING:claude-code-2===
(claude-code-2.md 完整内容)
DISPATCH_PROMPT

    local raw_output
    local exit_code=0
    raw_output=$(timeout "$DISPATCH_TIMEOUT" claude -p "$(cat "$prompt_file")" \
        --model opus \
        --dangerously-skip-permissions \
        2>"$dispatch_log") || exit_code=$?

    echo "$raw_output" > "$LOG_DIR/dispatch_raw_r${round_str}.md"

    if [ $exit_code -ne 0 ]; then
        if is_rate_limited "$dispatch_log"; then
            log "⚡ 调度中枢触发额度限制 (exit=$exit_code)"
            return 2
        fi
        log "❌ 调度中枢执行失败 (exit=$exit_code)"
        log "   stderr: $(head -5 "$dispatch_log" 2>/dev/null)"
        return 1
    fi

    if [ -z "$raw_output" ]; then
        if is_rate_limited "$dispatch_log"; then
            log "⚡ 调度中枢返回空输出（额度限制）"
            return 2
        fi
        log "❌ 调度中枢返回空输出"
        log "   stderr: $(head -5 "$dispatch_log" 2>/dev/null)"
        return 1
    fi

    export BRIEFING_DIR
    python3 - "$raw_output" << 'PYEOF'
import re, sys, os

output = sys.argv[1] if len(sys.argv) > 1 else sys.stdin.read()
briefing_dir = os.environ.get("BRIEFING_DIR", ".")

parts = re.split(r'===BRIEFING:(claude-code(?:-2)?)===', output)

wrote = 0
i = 1
while i < len(parts) - 1:
    name = parts[i].strip()
    content = parts[i+1].strip()
    content = re.split(r'\n===', content)[0].strip()
    path = os.path.join(briefing_dir, f"{name}.md")
    with open(path, "w") as f:
        f.write(content + "\n")
    print(f"WROTE:{name}")
    wrote += 1
    i += 2

if wrote == 0:
    print("PARSE_FAILED", file=sys.stderr)
    sys.exit(1)
PYEOF

    local parse_result=$?
    if [ $parse_result -ne 0 ]; then
        log "❌ Briefing 解析失败,原始输出见 dispatch_raw_r${round_str}.md"
        return 1
    fi

    _rl_consecutive=0
    log "📋 Briefing 已写入 $BRIEFING_DIR"
    return 0
}

run_worker() {
    local name="$1"
    local round="$2"
    local round_str
    round_str=$(printf '%03d' "$round")
    local briefing_file="$BRIEFING_DIR/${name}.md"
    local worker_log="$LOG_DIR/${name}_r${round_str}.log"
    local worker_stderr="$LOG_DIR/${name}_stderr_r${round_str}.log"

    if [ ! -f "$briefing_file" ]; then
        log "  ⏭️  $name: 无 briefing,跳过"
        return 0
    fi

    if grep -qi "无任务\|项目已完成\|无需继续" "$briefing_file" 2>/dev/null; then
        log "  ⏭️  $name: 本轮无任务"
        return 0
    fi

    log "  🚀 $name: 开始执行 (超时=${WORKER_TIMEOUT}s)"

    local briefing_content
    briefing_content=$(cat "$briefing_file")

    local worker_prompt="你是 worker ${name},在 gMKVExtractGUI macOS 端口项目中工作。
当前工作目录是 /Applications/gMKVExtractGUI

以下是你本轮的 briefing,请严格按 Task 顺序执行:

${briefing_content}

执行规则:
1. 严格遵守文件管辖约束,禁止修改不属于你的文件
2. 每个 Task 完成后再做下一个
3. 跑 dotnet build 自验证；编译错误必须修完
4. 【最重要】完成所有 Task 后,把完成报告写入文件:
   .orchestration/handoffs/${name}.md
5. 若某 Task 因依赖未到位 / 不可抗力被阻塞,在 handoff 中详细记录,不要卡住"

    timeout "$WORKER_TIMEOUT" claude -p "$worker_prompt" \
        --model sonnet \
        --dangerously-skip-permissions \
        > "$worker_log" 2>"$worker_stderr" || {
        local ec=$?

        if [ $ec -ne 124 ] && is_rate_limited "$worker_stderr"; then
            log "  ⚡ $name: 触发额度限制"
            cat > "$HANDOFF_DIR/${name}.md" << EOF
---
ai: $name
round: $round
status: rate_limited
finished_at: $(date -u '+%Y-%m-%dT%H:%M:%SZ')
---
## 额度限制
Worker 遭遇 Claude 额度限制，未完成任务。
建议: 等待额度恢复后重跑本轮。
日志: logs/${name}_stderr_r${round_str}.log
EOF
            return 2
        fi

        if [ $ec -eq 124 ]; then
            log "  ⏰ $name: 执行超时"
            cat > "$HANDOFF_DIR/${name}.md" << EOF
---
ai: $name
round: $round
status: timeout
finished_at: $(date -u '+%Y-%m-%dT%H:%M:%SZ')
---
## 执行超时
Worker 在 ${WORKER_TIMEOUT}s 内未完成。
建议: 下一轮拆分为更小的任务。
日志: logs/${name}_r${round_str}.log
EOF
        else
            log "  ❌ $name: 异常退出 (code=$ec)"
        fi
        return 0
    }

    if [ ! -f "$HANDOFF_DIR/${name}.md" ] || grep -q "status: pending" "$HANDOFF_DIR/${name}.md" 2>/dev/null; then
        log "  ⚠️  $name: handoff 未更新,从日志自动提取"
        cat > "$HANDOFF_DIR/${name}.md" << EOF
---
ai: $name
round: $round
status: done
finished_at: $(date -u '+%Y-%m-%dT%H:%M:%SZ')
note: handoff 由脚本从日志自动提取
---
## Worker 输出摘要
$(tail -80 "$worker_log")
EOF
    fi

    _rl_consecutive=0
    log "  ✅ $name: 完成"
    return 0
}

check_done() {
    for f in "$BRIEFING_DIR"/claude-code.md "$BRIEFING_DIR"/claude-code-2.md; do
        [ -f "$f" ] || continue
        if ! grep -qi "项目已完成\|无需继续" "$f" 2>/dev/null; then
            return 1
        fi
    done
    return 0
}

log "════════════════════════════════════════"
log "🌙 gMKVExtractGUI macOS 端口·夜间调度启动"
log "  项目:    $PROJECT_DIR"
log "  最大轮次: $MAX_ROUNDS"
log "  Worker超时: ${WORKER_TIMEOUT}s ($(( WORKER_TIMEOUT / 60 ))min)"
log "  限额等待: ${RATE_LIMIT_WAIT}s ($(( RATE_LIMIT_WAIT / 60 ))min) 起，最大 $(( RATE_LIMIT_MAX_WAIT / 60 ))min"
log "  停止方式: touch $STOP_FILE"
log "════════════════════════════════════════"

cd "$PROJECT_DIR" || { log "❌ 无法进入项目目录"; exit 1; }

for round in $(seq 1 "$MAX_ROUNDS"); do

    if [ -f "$STOP_FILE" ]; then
        log "🛑 检测到 STOP,优雅退出"
        rm -f "$STOP_FILE"
        break
    fi

    log "━━━━━━━━━━ Round $round / $MAX_ROUNDS ━━━━━━━━━━"

    local_dispatch_ok=false
    for _attempt in 1 2 3; do
        dispatch_result=0
        run_dispatch "$round" || dispatch_result=$?

        if [ $dispatch_result -eq 0 ]; then
            local_dispatch_ok=true
            break
        elif [ $dispatch_result -eq 2 ]; then
            do_rate_limit_wait
        else
            log "⚠️  调度失败，30s 后重试 (attempt $_attempt/3)..."
            sleep 30
        fi
    done

    if [ "$local_dispatch_ok" = false ]; then
        log "❌ 调度连续失败 3 次，跳过本轮"
        continue
    fi

    if check_done; then
        log "🎉 调度中枢判定项目已完成！"
        break
    fi

    worker_rl=false
    run_worker "claude-code" "$round" &
    pid1=$!
    run_worker "claude-code-2" "$round" &
    pid2=$!

    wait $pid1; ec1=$?
    wait $pid2; ec2=$?

    if [ $ec1 -eq 2 ] || [ $ec2 -eq 2 ]; then
        worker_rl=true
    fi

    log "📦 Round $round 完成"

    if [ "$worker_rl" = true ]; then
        log "⚡ Worker 遭遇额度限制，退避后将重跑本轮 worker..."
        do_rate_limit_wait
        run_worker "claude-code" "$round" &
        pid1=$!
        run_worker "claude-code-2" "$round" &
        pid2=$!
        wait $pid1 || true
        wait $pid2 || true
        log "📦 Round $round 重跑完成"
    fi

    if [ "$round" -lt "$MAX_ROUNDS" ]; then
        log "💤 ${SLEEP_BETWEEN}s 后开始下一轮..."
        sleep "$SLEEP_BETWEEN"
    fi
done

log "════════════════════════════════════════"
log "🌅 夜间调度结束"
log "  完成轮次: $round"
log "  查看结果: ls $HANDOFF_DIR"
log "  查看日志: ls $LOG_DIR"
log "════════════════════════════════════════"
