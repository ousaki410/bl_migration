#!/usr/bin/env bash
# WarbandPort Linux 启动脚本
# 用法: ./run.sh [port-warband|test|decompile|api-snapshot|api-diff] [选项...]

set -euo pipefail

# ========== 配置区域 ==========
PROJECT_ROOT="/home/ou/Projects/re-bannerlord/blmodtool"
DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
CLI_DLL="${PROJECT_ROOT}/src/BlModTool.Cli/bin/Release/net8.0/BlModTool.Cli.dll"

# 战团 Native 目录（按需修改）
NATIVE_DIR="/home/ou/Projects/re-mb-1011/Modules/Native"

# Bannerlord 安装目录（安装模式时需要，按需修改）
BANNERLORD_DIR="/home/ou/Projects/re-bannerlord/Mount.and.Blade.II.Bannerlord.v1.3.15(Danjipai.com)"

# 输出目录
OUTPUT_DIR="/tmp/warband-port"

# 模块名称
MODULE_NAME="WarbandPort"

# ========== 内部变量 ==========
export DOTNET_ROOT="${DOTNET_ROOT}"
export PATH="${DOTNET_ROOT}:${PATH}"
export TMPDIR="${PROJECT_ROOT}/.tmp"

# ========== 工具函数 ==========
log() { echo -e "\033[1;32m[$(date '+%H:%M:%S')]\033[0m $*"; }
err() { echo -e "\033[1;31m[$(date '+%H:%M:%S')] ERROR:\033[0m $*" >&2; }
die() { err "$*"; exit 1; }

check_prereqs() {
    [[ -x "${DOTNET_ROOT}/dotnet" ]] || die "未找到 dotnet: ${DOTNET_ROOT}/dotnet"
    [[ -f "${CLI_DLL}" ]] || die "未找到编译产物: ${CLI_DLL}\n请先运行: ./run.sh build"
    [[ -d "${NATIVE_DIR}" ]] || die "战团 Native 目录不存在: ${NATIVE_DIR}"
}

run_dotnet() {
    "${DOTNET_ROOT}/dotnet" "$@"
}

# ========== 子命令 ==========
cmd_build() {
    log "构建项目..."
    cd "${PROJECT_ROOT}"
    run_dotnet build BlModTool.sln -c Release --no-incremental
    log "构建完成"
}

cmd_test() {
    log "运行测试..."
    cd "${PROJECT_ROOT}"
    run_dotnet test BlModTool.sln -c Release "${@}"
}

cmd_port() {
    check_prereqs

    local args=(
        port-warband
        --native "${NATIVE_DIR}"
        --output "${OUTPUT_DIR}"
    )

    # 可选功能（通过环境变量或参数控制）
    [[ "${WITH_SUBMODULE:-1}" == "1" ]] && args+=(--submodule)
    [[ "${WITH_README:-1}" == "1" ]] && args+=(--readme)
    [[ "${WITH_PARTY_TEMPLATES:-1}" == "1" ]] && args+=(--party-templates)
    [[ "${WITH_SETTLEMENTS:-1}" == "1" ]] && args+=(--settlements)
    [[ "${WITH_QUESTS:-1}" == "1" ]] && args+=(--quests)
    [[ "${WITH_MENUS:-1}" == "1" ]] && args+=(--menus)
    [[ "${WITH_DIALOG_STATES:-1}" == "1" ]] && args+=(--dialog-states)
    [[ "${WITH_HEROES:-0}" == "1" ]] && args+=(--include-heroes)
    [[ "${WITH_KINGDOMS:-0}" == "1" ]] && args+=(--emit-kingdoms)
    [[ "${KEEP_MESHES:-0}" == "1" ]] && args+=(--keep-meshes)

    log "开始转换..."
    log "输入: ${NATIVE_DIR}"
    log "输出: ${OUTPUT_DIR}"
    log "参数: ${args[*]}"

    run_dotnet "${CLI_DLL}" "${args[@]}"
    log "转换完成: ${OUTPUT_DIR}"
}

cmd_install() {
    check_prereqs

    [[ -d "${BANNERLORD_DIR}" ]] || die "Bannerlord 目录不存在: ${BANNERLORD_DIR}"

    local args=(
        port-warband
        --native "${NATIVE_DIR}"
        --output "${OUTPUT_DIR}"
        --install
        --bannerlord-dir "${BANNERLORD_DIR}"
        --module-name "${MODULE_NAME}"
        --submodule --readme
        --party-templates --settlements --quests --menus --dialog-states
    )

    log "转换并安装到 Bannerlord..."
    log "目标目录: ${BANNERLORD_DIR}/Modules/${MODULE_NAME}"

    run_dotnet "${CLI_DLL}" "${args[@]}"
    log "安装完成"
}

cmd_decompile() {
    check_prereqs
    run_dotnet "${CLI_DLL}" decompile "${@}"
}

cmd_api_snapshot() {
    check_prereqs
    run_dotnet "${CLI_DLL}" api-snapshot "${@}"
}

cmd_api_diff() {
    check_prereqs
    run_dotnet "${CLI_DLL}" api-diff "${@}"
}

cmd_package() {
    check_prereqs
    run_dotnet "${CLI_DLL}" package "${@}"
}

cmd_wizard() {
    check_prereqs
    run_dotnet "${CLI_DLL}" wizard "${@}"
}

cmd_clean() {
    log "清理构建产物..."
    rm -rf "${PROJECT_ROOT}/src/BlModTool.Cli/bin" "${PROJECT_ROOT}/src/BlModTool.Cli/obj"
    rm -rf "${PROJECT_ROOT}/tests/BlModTool.Cli.Tests/bin" "${PROJECT_ROOT}/tests/BlModTool.Cli.Tests/obj"
    log "清理完成"
}

cmd_all() {
    log "===== 一键全流程：构建 -> 测试 -> 完整转换 -> 安装 ====="
    cmd_build
    cmd_test
    cmd_port
    cmd_install
    log "===== 全流程完成 ====="
}

cmd_help() {
    cat <<'EOF'
WarbandPort Linux 启动脚本

用法: ./run.sh <命令> [选项...]

命令:
  build                    构建项目 (Release)
  test [xunit参数...]      运行测试
  port [选项...]           转换战团数据为霸王 XML
  install                  转换并安装到 Bannerlord Modules 目录
  all                      一键全流程：构建 -> 测试 -> 完整转换 -> 安装
  decompile [参数...]      反编译 TaleWorlds DLL
  api-snapshot [参数...]   导出 API 快照
  api-diff [参数...]       对比 API 快照
  package [选项...]        打包模块目录为 .zip/.mb 分发包 (M17)
    --input, -i <目录>     要打包的模块目录（必填）
    --output, -o <文件>    输出打包文件路径（默认 ./<目录名>.zip）
    --format, -f <zip|mb>  打包格式（默认 zip）
  wizard                   交互式向导，逐步收集参数并执行转换 (M18)
  clean                    清理构建产物
  help                     显示此帮助

环境变量 (port/install 命令):
  WITH_SUBMODULE=1         生成 SubModule.xml (默认 1)
  WITH_README=1            生成 README.md (默认 1)
  WITH_PARTY_TEMPLATES=1   转换 party_templates.txt (默认 1)
  WITH_SETTLEMENTS=1       转换 settlements (默认 1)
  WITH_QUESTS=1            转换 quests (默认 1)
  WITH_MENUS=1             转换 menus (默认 1)
  WITH_DIALOG_STATES=1     转换 dialog_states (默认 1)
  WITH_HEROES=1            包含英雄兵种 (默认 0)
  WITH_KINGDOMS=1          生成 kingdoms (默认 0)
  KEEP_MESHES=1            保留原始网格名 (默认 0)

配置文件: 编辑脚本顶部的配置区域修改路径

示例:
  ./run.sh build
  ./run.sh test
  ./run.sh port
  ./run.sh install
  ./run.sh all
  WITH_QUESTS=0 WITH_MENUS=0 ./run.sh port
EOF
}

# ========== 内部函数 ==========
log() { echo -e "\033[1;32m[$(date '+%H:%M:%S')]\033[0m $*"; }
err() { echo -e "\033[1;31m[$(date '+%H:%M:%S')] ERROR:\033[0m $*" >&2; }
die() { err "$*"; exit 1; }

check_prereqs() {
    [[ -x "${DOTNET_ROOT}/dotnet" ]] || die "未找到 dotnet: ${DOTNET_ROOT}/dotnet"
    [[ -f "${CLI_DLL}" ]] || die "未找到编译产物: ${CLI_DLL}\n请先运行: ./run.sh build"
    [[ -d "${NATIVE_DIR}" ]] || die "战团 Native 目录不存在: ${NATIVE_DIR}"
}

run_dotnet() {
    "${DOTNET_ROOT}/dotnet" "$@"
}

# ========== 主入口 ==========
main() {
    local cmd="${1:-help}"
    shift || true

    case "${cmd}" in
        build)    cmd_build ;;
        test)     cmd_test "$@" ;;
        port)     cmd_port "$@" ;;
        install)  cmd_install "$@" ;;
        all)      cmd_all "$@" ;;
        decompile) cmd_decompile "$@" ;;
        api-snapshot) cmd_api_snapshot "$@" ;;
        api-diff) cmd_api_diff "$@" ;;
        package) cmd_package "$@" ;;
        wizard) cmd_wizard "$@" ;;
        clean)    cmd_clean ;;
        help|--help|-h) cmd_help ;;
        *) die "未知命令: ${cmd}\n运行 './run.sh help' 查看帮助" ;;
    esac
}

main "$@"
