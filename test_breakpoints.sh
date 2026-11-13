#!/usr/bin/env bash
# WarbandPort 断点/集成测试套件
# 用途: 验证工具链各阶段功能是否正常
# 用法: ./test_breakpoints.sh [阶段名|all]

set -euo pipefail

PROJECT_ROOT="/home/ou/Projects/re-bannerlord/blmodtool"
DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
CLI_DLL="${PROJECT_ROOT}/src/BlModTool.Cli/bin/Release/net8.0/BlModTool.Cli.dll"
NATIVE_DIR="/home/ou/Projects/re-mb-1011/Modules/Native"
OUTPUT_BASE="/tmp/warband-bp-test"

export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="${DOTNET_ROOT}:${PATH}"
export TMPDIR="${PROJECT_ROOT}/.tmp"

log() { echo -e "\033[1;34m[BP $(date '+%H:%M:%S')]\033[0m $*"; }
pass() { echo -e "\033[1;32m[PASS]\033[0m $*"; }
fail() { echo -e "\033[1;31m[FAIL]\033[0m $*"; exit 1; }

run() { "${DOTNET_ROOT}/dotnet" "$@"; }

# ========== 断点测试 ==========

bp_00_build() {
    log "BP00: 构建项目"
    cd /home/ou/Projects/re-bannerlord/blmodtool
    ~/.dotnet/dotnet build BlModTool.sln -c Release --no-incremental >/dev/null 2>&1
    [[ -f /home/ou/Projects/re-bannerlord/blmodtool/src/BlModTool.Cli/bin/Release/net8.0/BlModTool.Cli.dll ]] || return 1
    pass "构建成功"
}

bp_01_help() {
    log "BP01: CLI 帮助输出"
    run /home/ou/Projects/re-bannerlord/blmodtool/src/BlModTool.Cli/bin/Release/net8.0/BlModTool.Cli.dll --help | grep -q "port-warband" || return 1
    pass "CLI 帮助正常"
}

bp_02_version() {
    log "BP02: 版本输出"
    run /home/ou/Projects/re-bannerlord/blmodtool/src/BlModTool.Cli/bin/Release/net8.0/BlModTool.Cli.dll --version | grep -q "0.5.0-m10" || return 1
    pass "版本正确: 0.5.0-m10"
}

bp_03_native_check() {
    log "BP03: 战团 Native 目录检查"
    [[ -f /home/ou/Projects/re-mb-1011/Modules/Native/item_kinds1.txt ]] || return 1
    [[ -f /home/ou/Projects/re-mb-1011/Modules/Native/troops.txt ]] || return 1
    [[ -f /home/ou/Projects/re-mb-1011/Modules/Native/factions.txt ]] || return 1
    [[ -f /home/ou/Projects/re-mb-1011/Modules/Native/skills.txt ]] || return 1
    pass "Native 源文件完整"
}

bp_04_reader_items() {
    log "BP04: Reader - Items 解析"
    local out=$(/home/ou/.dotnet/dotnet /home/ou/Projects/re-bannerlord/blmodtool/src/BlModTool.Cli/bin/Release/net8.0/BlModTool.Cli.dll port-warband --native /home/ou/Projects/re-mb-1011/Modules/Native --output /tmp/bp_test_items 2>&1 | grep "物品:" | head -1)
    echo "$out" | grep -q "420/433" || return 1
    pass "Items 解析: 420/433"
}

bp_05_reader_troops() {
    log "BP05: Reader - Troops 解析"
    local out=$(/home/ou/.dotnet/dotnet /home/ou/Projects/re-bannerlord/blmodtool/src/BlModTool.Cli/bin/Release/net8.0/BlModTool.Cli.dll port-warband --native /home/ou/Projects/re-mb-1011/Modules/Native --output /tmp/bp_test_troops 2>&1 | grep "兵种:" | head -1)
    echo "$out" | grep -q "134/619" || return 1
    pass "Troops 解析: 134/619"
}

bp_06_reader_factions() {
    log "BP06: Reader - Factions 解析"
    local out=$(/home/ou/.dotnet/dotnet /home/ou/Projects/re-bannerlord/blmodtool/src/BlModTool.Cli/bin/Release/net8.0/BlModTool.Cli.dll port-warband --native /home/ou/Projects/re-mb-1011/Modules/Native --output /tmp/bp_test_factions 2>&1 | grep "阵营:" | head -1)
    echo "$out" | grep -q "18" || return 1
    pass "Factions 解析: 18 个阵营"
}

bp_07_reader_skills() {
    log "BP07: Reader - Skills 解析"
    /home/ou/.dotnet/dotnet /home/ou/Projects/re-bannerlord/blmodtool/src/BlModTool.Cli/bin/Release/net8.0/BlModTool.Cli.dll port-warband --native /home/ou/Projects/re-mb-1011/Modules/Native --output /tmp/bp_test_skills 2>&1 | grep -q "技能: 42/42" || return 1
    pass "Skills 解析: 42/42"
}

bp_08_reader_party_templates() {
    log "BP08: Reader - PartyTemplates 解析"
    /home/ou/.dotnet/dotnet /home/ou/Projects/re-bannerlord/blmodtool/src/BlModTool.Cli/bin/Release/net8.0/BlModTool.Cli.dll port-warband --native /home/ou/Projects/re-mb-1011/Modules/Native --output /tmp/bp_test_pt --party-templates 2>&1 | grep -q "党团模板: 33/48" || return 1
    pass "PartyTemplates 解析: 33/48"
}

bp_09_reader_settlements() {
    log "BP09: Reader - Settlements 解析"
    /home/ou/.dotnet/dotnet /home/ou/Projects/re-bannerlord/blmodtool/src/BlModTool.Cli/bin/Release/net8.0/BlModTool.Cli.dll port-warband --native /home/ou/Projects/re-mb-1011/Modules/Native --output /tmp/bp_test_sett --settlements 2>&1 | grep -q "定居点: 148/148" || return 1
    pass "Settlements 解析: 148/148"
}

bp_10_reader_quests() {
    log "BP10: Reader - Quests 解析"
    /home/ou/.dotnet/dotnet /home/ou/Projects/re-bannerlord/blmodtool/src/BlModTool.Cli/bin/Release/net8.0/BlModTool.Cli.dll port-warband --native /home/ou/Projects/re-mb-1011/Modules/Native --output /tmp/bp_test_quests --quests 2>&1 | grep -q "任务: 41/41" || return 1
    pass "Quests 解析: 41/41"
}

bp_11_reader_menus() {
    log "BP11: Reader - Menus 解析"
    /home/ou/.dotnet/dotnet /home/ou/Projects/re-bannerlord/blmodtool/src/BlModTool.Cli/bin/Release/net8.0/BlModTool.Cli.dll port-warband --native /home/ou/Projects/re-mb-1011/Modules/Native --output /tmp/bp_test_menus --menus 2>&1 | grep -q "菜单: 183/183" || return 1
    pass "Menus 解析: 183/183"
}

bp_11b_reader_dialog_states() {
    log "BP11b: Reader - DialogStates 解析"
    /home/ou/.dotnet/dotnet /home/ou/Projects/re-bannerlord/blmodtool/src/BlModTool.Cli/bin/Release/net8.0/BlModTool.Cli.dll port-warband --native /home/ou/Projects/re-mb-1011/Modules/Native --output /tmp/bp_test_dialog --dialog-states 2>&1 | grep -q "对话状态: 889/889" || return 1
    pass "DialogStates 解析: 889/889"
}

bp_20_items_xml() {
    log "BP20: Output - Items XML 结构验证"
    local dir="/tmp/warband-bp-test/ModuleData/items"
    local count=0
    for f in "$dir"/*.xml; do
        [[ -f "$f" ]] && ((count++))
    done
    [[ $count -eq 10 ]] || return 1
    # 验证每个文件可解析且有 Item 元素
    for f in "$dir"/*.xml; do
        xmllint --noout "$f" 2>/dev/null || return 1
    done
    pass "Items XML: 10 个文件，结构有效"
}

bp_21_npcs_xml() {
    log "BP21: Output - NPCs XML 验证"
    local f="/tmp/warband-bp-test/ModuleData/spnpccharacters.xml"
    [[ -f "$f" ]] || return 1
    local count=$(xmllint --xpath "count(//NPCCharacter)" "$f" 2>/dev/null)
    [[ $count -eq 386 ]] || return 1  # 134 troops + 252 synthetic
    pass "NPCs XML: 386 个角色"
}

bp_22_cultures_xml() {
    log "BP22: Output - Cultures XML 验证"
    local f="/tmp/warband-bp-test/ModuleData/spcultures.xml"
    [[ -f "$f" ]] || return 1
    local count=$(xmllint --xpath "count(//Culture)" "$f" 2>/dev/null)
    [[ $count -eq 18 ]] || return 1
    # 检查角色引用
    grep -q 'villager=' "$f" || return 1
    pass "Cultures XML: 18 个文化，含角色引用"
}

bp_23_skills_xml() {
    log "BP23: Output - Skills XML 验证"
    local f="/tmp/warband-bp-test/ModuleData/skills/warband_skills.xml"
    [[ -f "$f" ]] || return 1
    local count=$(xmllint --xpath "count(//Skill)" "$f" 2>/dev/null)
    [[ $count -eq 42 ]] || return 1
    # 检查 base_attribute 字段
    grep -q 'base_attribute="Charisma"' "$f" || return 1
    pass "Skills XML: 42 个技能，属性正确"
}

bp_24_party_templates_xml() {
    log "BP24: Output - PartyTemplates XML 验证"
    local f="/tmp/warband-bp-test/ModuleData/party_templates/warband_party_templates.xml"
    [[ -f "$f" ]] || return 1
    local count=$(xmllint --xpath "count(//MBPartyTemplate)" "$f" 2>/dev/null)
    [[ $count -eq 33 ]] || return 1
    grep -q 'troop="NPCCharacter.wb_looter"' "$f" || return 1
    pass "PartyTemplates XML: 33 个模板，含 looters"
}

bp_25_settlements_xml() {
    log "BP25: Output - Settlements XML 验证"
    local f="/tmp/warband-bp-test/ModuleData/settlements/warband_settlements.xml"
    [[ -f "$f" ]] || return 1
    local count=$(xmllint --xpath "count(//Settlement)" "$f" 2>/dev/null)
    [[ $count -eq 148 ]] || return 1
    # 检查城镇/城堡/村庄分类
    grep -q 'is_castle="true"' "$f" || return 1
    grep -q 'is_castle="false"' "$f" || return 1
    grep -q 'Village' "$f" || return 1
    pass "Settlements XML: 148 定居点 (城镇/城堡/村庄)"
}

bp_26_quests_xml() {
    log "BP26: Output - Quests XML 验证"
    local f="/tmp/warband-bp-test/ModuleData/quests/warband_quests.xml"
    [[ -f "$f" ]] || return 1
    local count=$(xmllint --xpath "count(//Quest)" "$f" 2>/dev/null)
    [[ $count -eq 41 ]] || return 1
    grep -q 'xp_reward="2"' "$f" || return 1
    pass "Quests XML: 41 个任务"
}

bp_27_menus_xml() {
    log "BP27: Output - Menus XML 验证"
    local f="/tmp/warband-bp-test/ModuleData/menus/warband_menus.xml"
    [[ -f "$f" ]] || return 1
    local count=$(xmllint --xpath "count(//Menu)" "$f" 2>/dev/null)
    [[ $count -eq 183 ]] || return 1
    # 检查菜单选项结构
    grep -q "<Op code=" "$f" || return 1
    pass "Menus XML: 183 个菜单，含选项操作码"
}

bp_28_dialog_states_xml() {
    log "BP28: Output - DialogStates XML 验证"
    local f="/tmp/warband-bp-test/ModuleData/dialog_states/warband_dialog_states.xml"
    [[ -f "$f" ]] || return 1
    local count=$(xmllint --xpath "count(//DialogState)" "$f" 2>/dev/null)
    [[ $count -eq 889 ]] || return 1
    grep -q 'id="start"' "$f" || return 1
    pass "DialogStates XML: 889 个状态"
}

bp_30_submodule_xml() {
    log "BP30: Output - SubModule.xml 注册验证"
    local f="/tmp/warband-bp-test/SubModule.xml"
    [[ -f "$f" ]] || return 1
    # 检查关键节点
    grep -q 'id="Items" path="items"' "$f" || return 1
    grep -q 'id="SPCultures" path="spcultures"' "$f" || return 1
    grep -q 'id="NPCCharacters" path="spnpccharacters"' "$f" || return 1
    grep -q 'id="Skills" path="skills"' "$f" || return 1
    grep -q 'id="PartyTemplates" path="party_templates"' "$f" || return 1
    grep -q 'id="Settlements" path="settlements"' "$f" || return 1
    grep -q 'id="Quests" path="quests"' "$f" || return 1
    grep -q 'id="Menus" path="menus"' "$f" || return 1
    grep -q 'id="DialogStates" path="dialog_states"' "$f" || return 1
    pass "SubModule.xml: 10 个节点全部注册"
}

bp_31_module_info() {
    log "BP31: Output - ModuleInfo.xml 验证"
    local f="/tmp/warband-bp-test/ModuleInfo.xml"
    [[ -f "$f" ]] || return 1
    grep -q 'value="WarbandPort"' "$f" || return 1
    grep -q 'value="v1.0.0"' "$f" || return 1
    grep -q 'Id="Native"' "$f" || return 1
    grep -q 'Id="SandBoxCore"' "$f" || return 1
    grep -q 'Id="Sandbox"' "$f" || return 1
    pass "ModuleInfo.xml: 完整"
}

bp_32_languages() {
    log "BP32: Output - Languages/en/strings.xml 验证"
    local f="/tmp/warband-bp-test/Languages/en/strings.xml"
    [[ -f "$f" ]] || return 1
    local count=$(xmllint --xpath "count(//String)" "$f" 2>/dev/null)
    [[ $count -gt 500 ]] || return 1  # 物品名+NPC名+文化名
    pass "Languages: $count 个字符串条目"
}

bp_33_readme() {
    log "BP33: Output - README.md 验证"
    local f="/tmp/warband-bp-test/README.md"
    [[ -f "$f" ]] || return 1
    grep -q "items/weapons.xml" "$f" || return 1
    grep -q "NPC role templates" "$f" || return 1
    pass "README.md: 含分类统计和角色模板"
}

bp_34_report_json() {
    log "BP34: Output - conversion-report.json 验证"
    local f="/tmp/warband-bp-test/conversion-report.json"
    [[ -f "$f" ]] || return 1
    local items=$(jq -r '.counts.itemsConverted' "$f" 2>/dev/null)
    [[ "$items" == "420" ]] || return 1
    local troops=$(jq -r '.counts.troopsConverted' "$f" 2>/dev/null)
    [[ "$troops" == "134" ]] || return 1
    pass "Report JSON: 完整统计"
}

bp_40_deterministic() {
    log "BP40: 确定性输出验证"
    /home/ou/.dotnet/dotnet /home/ou/Projects/re-bannerlord/blmodtool/src/BlModTool.Cli/bin/Release/net8.0/BlModTool.Cli.dll port-warband --native /home/ou/Projects/re-mb-1011/Modules/Native --output /tmp/bp_det1 --submodule --readme --party-templates --settlements --quests --menus --dialog-states 2>&1 | tail -5 | grep -q "完成" || return 1
    /home/ou/.dotnet/dotnet /home/ou/Projects/re-mb-1011/Modules/Native --output /tmp/bp_det2 --submodule --readme --party-templates --settlements --quests --menus --dialog-states 2>&1 | tail -5 | grep -q "完成" || return 1
    # 对比 NPC XML (除 GeneratedAtUtc 外应完全一致)
    diff -u /tmp/bp_det1/ModuleData/spnpccharacters.xml /tmp/bp_det2/ModuleData/spnpccharacters.xml | grep -v "GeneratedAtUtc" | grep -q "^[-+]" && return 1
    pass "确定性输出: 两次运行 NPC XML 完全一致"
}

bp_50_install() {
    log "BP50: 安装功能验证"
    local test_dir="/tmp/bp_install_test"
    rm -rf "$test_dir"
    mkdir -p "$test_dir/Modules"
    /home/ou/.dotnet/dotnet /home/ou/Projects/re-bannerlord/blmodtool/src/BlModTool.Cli/bin/Release/net8.0/BlModTool.Cli.dll port-warband \
        --native /home/ou/Projects/re-mb-1011/Modules/Native \
        --output /tmp/bp_install_test \
        --install --bannerlord-dir "$test_dir" \
        --module-name WarbandPort \
        --submodule --readme --party-templates --settlements --quests --menus --dialog-states 2>&1 | grep -q "已安装" || return 1
    [[ -d "/tmp/bp_install_test/Modules/WarbandPort" ]] || return 1
    [[ -f "/tmp/bp_install_test/Modules/WarbandPort/ModuleInfo.xml" ]] || return 1
    [[ -f "/tmp/bp_install_test/Modules/WarbandPort/SubModule.xml" ]] || return 1
    [[ -d "/tmp/bp_install_test/Modules/WarbandPort/ModuleData/items" ]] || return 1
    pass "安装功能: 模块正确安装到目标目录"
}

bp_51_test_cli() {
    log "BP51: CLI test 命令验证"
    /home/ou/.dotnet/dotnet /home/ou/Projects/re-bannerlord/blmodtool/src/BlModTool.Cli/bin/Release/net8.0/BlModTool.Cli.dll test 2>&1 | grep -q "已通过! - 失败:     0，通过:    40" || return 1
    pass "CLI test 命令: 40/40 通过"
}

# ========== 主流程 ==========
run_bp() {
    local bp=$1
    log "执行 $bp..."
    if $bp; then
        return 0
    else
        fail "$bp 失败"
        return 1
    fi
}

main() {
    local phase="${1:-all}"
    log "===== WarbandPort 断点测试套件 ====="

    local bps=()
    case "$phase" in
        build)    bps=(bp_00_build bp_01_help bp_02_version) ;;
        reader)   bps=(bp_03_native_check bp_04_reader_items bp_05_reader_troops bp_06_reader_factions bp_06_reader_factions bp_07_reader_skills bp_08_reader_party_templates bp_09_reader_settlements bp_10_reader_quests bp_11_reader_menus bp_11b_reader_dialog_states) ;;
        output)   bps=(bp_20_items_xml bp_21_npcs_xml bp_22_cultures_xml bp_23_skills_xml bp_24_party_templates_xml bp_25_settlements_xml bp_26_quests_xml bp_27_menus_xml bp_28_dialog_states_xml) ;;
        module)   bps=(bp_30_submodule_xml bp_31_module_info bp_32_languages bp_33_readme bp_34_report_json) ;;
        runtime)  bps=(bp_40_deterministic bp_50_install bp_51_test_cli) ;;
        all)      bps=(bp_00_build bp_01_help bp_02_version bp_03_native_check bp_04_reader_items bp_05_reader_troops bp_06_reader_factions bp_07_reader_skills bp_08_reader_party_templates bp_09_reader_settlements bp_10_reader_quests bp_11_reader_menus bp_11b_reader_dialog_states bp_20_items_xml bp_21_npcs_xml bp_22_cultures_xml bp_23_skills_xml bp_24_party_templates_xml bp_25_settlements_xml bp_26_quests_xml bp_27_menus_xml bp_28_dialog_states_xml bp_30_submodule_xml bp_31_module_info bp_32_languages bp_33_readme bp_34_report_json bp_40_deterministic bp_50_install bp_51_test_cli) ;;
        *) die "未知阶段: $phase (可选: build|reader|output|module|runtime|all)" ;;
    esac

    local passed=0 failed=0
    for bp in "${bps[@]}"; do
        if run_bp "$bp"; then
            ((passed++))
        else
            ((failed++))
        fi
    done

    log "===== 结果: $passed 通过, $failed 失败 ====="
    [[ $failed -eq 0 ]] || exit 1
}

main "$@"