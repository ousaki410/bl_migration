#!/usr/bin/env bash
# WarbandPort 断点测试 - 简化版
set -euo pipefail

PROJECT_ROOT="/home/ou/Projects/re-bannerlord/blmodtool"
DOTNET="${DOTNET_ROOT:-$HOME/.dotnet}/dotnet"
CLI_DLL="${PROJECT_ROOT}/src/BlModTool.Cli/bin/Release/net8.0/BlModTool.Cli.dll"
NATIVE_DIR="/home/ou/Projects/re-mb-1011/Modules/Native"
TEST_OUTPUT_DIR="/tmp/warband-bp-test"

run() { "$DOTNET" "$@"; }

# 先运行一次完整转换，生成所有测试需要的文件
setup() {
    echo "Setting up full conversion..."
    rm -rf "$TEST_OUTPUT_DIR"
    run "$CLI_DLL" port-warband \
        --native "$NATIVE_DIR" \
        --output "$TEST_OUTPUT_DIR" \
        --submodule --readme \
        --party-templates --settlements --quests --menus --dialog-states >/dev/null 2>&1
}

# ========== 断点测试 ==========

test_build() {
    echo "Testing: build"
    cd /home/ou/Projects/re-bannerlord/blmodtool
    ~/.dotnet/dotnet build BlModTool.sln -c Release --no-incremental >/dev/null 2>&1
    [[ -f "$CLI_DLL" ]] || return 1
    echo "  Build: OK"
}

test_help() {
    echo "Testing: help"
    run "$CLI_DLL" --help | grep -q "port-warband" || return 1
}

test_version() {
    echo "Testing: version"
    run "$CLI_DLL" --version | grep -q "0.5.0-m10" || return 1
}

test_native() {
    echo "Testing: native dir"
    [[ -f "$NATIVE_DIR/item_kinds1.txt" ]] || return 1
    [[ -f "$NATIVE_DIR/troops.txt" ]] || return 1
    [[ -f "$NATIVE_DIR/factions.txt" ]] || return 1
    [[ -f "$NATIVE_DIR/skills.txt" ]] || return 1
}

test_items() {
    echo "Testing: items parsing"
    run "$CLI_DLL" port-warband --native "$NATIVE_DIR" --output /tmp/bp_items 2>&1 | grep -q "物品: 420/433"
}

test_troops() {
    echo "Testing: troops parsing"
    run "$CLI_DLL" port-warband --native "$NATIVE_DIR" --output /tmp/bp_troops 2>&1 | grep -q "兵种: 134/619"
}

test_factions() {
    echo "Testing: factions parsing"
    run "$CLI_DLL" port-warband --native "$NATIVE_DIR" --output /tmp/bp_factions 2>&1 | grep -q "阵营"
}

test_skills() {
    echo "Testing: skills parsing"
    run "$CLI_DLL" port-warband --native "$NATIVE_DIR" --output /tmp/bp_skills 2>&1 | grep -q "技能: 42/42"
}

test_party_templates() {
    echo "Testing: party templates"
    run "$CLI_DLL" port-warband --native "$NATIVE_DIR" --output /tmp/bp_pt --party-templates 2>&1 | grep -q "党团模板: 33/48"
}

test_settlements() {
    echo "Testing: settlements parsing"
    run "$CLI_DLL" port-warband --native "$NATIVE_DIR" --output /tmp/bp_sett --settlements 2>&1 | grep -q "定居点: 148/148"
}

test_quests() {
    echo "Testing: quests parsing"
    run "$CLI_DLL" port-warband --native "$NATIVE_DIR" --output /tmp/bp_quests --quests 2>&1 | grep -q "任务: 41/41"
}

test_menus() {
    echo "Testing: menus parsing"
    run "$CLI_DLL" port-warband --native "$NATIVE_DIR" --output /tmp/bp_menus --menus 2>&1 | grep -q "菜单: 183/183"
}

test_dialog() {
    echo "Testing: dialog states"
    run "$CLI_DLL" port-warband --native "$NATIVE_DIR" --output /tmp/bp_dialog --dialog-states 2>&1 | grep -q "对话状态: 889/889"
}

# ========== XML 验证测试 (使用统一输出目录) ==========

has_xmllint() { command -v xmllint >/dev/null 2>&1; }

test_xml_items() {
    echo "Testing: items XML"
    local count=0
    for f in "$TEST_OUTPUT_DIR/ModuleData/items"/*.xml; do
        [[ -f "$f" ]] && ((count++))
    done
    [[ $count -eq 10 ]] || return 1
    if command -v xmllint >/dev/null 2>&1; then
        for f in "$TEST_OUTPUT_DIR/ModuleData/items"/*.xml; do
            xmllint --noout "$f" 2>/dev/null || return 1
        done
    else
        echo "  (skipping XML validation - xmllint not available)"
    fi
}

test_xml_npcs() {
    echo "Testing: NPCs XML"
    local f="$TEST_OUTPUT_DIR/ModuleData/spnpccharacters.xml"
    [[ -f "$f" ]] || return 1
    if has_xmllint; then
        local count=$(xmllint --xpath "count(//NPCCharacter)" "$f" 2>/dev/null)
        [[ "$count" -eq 386 ]] || return 1
    else
        echo "  (skipping xpath validation - xmllint not available)"
    fi
}

test_xml_cultures() {
    echo "Testing: cultures XML"
    local f="$TEST_OUTPUT_DIR/ModuleData/spcultures.xml"
    [[ -f "$f" ]] || return 1
    if has_xmllint; then
        local count=$(xmllint --xpath "count(//Culture)" "$f" 2>/dev/null)
        [[ "$count" -eq 18 ]] || return 1
    else
        echo "  (skipping xpath validation - xmllint not available)"
    fi
    grep -q 'villager=' "$f"
}

test_xml_skills() {
    echo "Testing: skills XML"
    local f="$TEST_OUTPUT_DIR/ModuleData/skills/warband_skills.xml"
    [[ -f "$f" ]] || return 1
    if has_xmllint; then
        local count=$(xmllint --xpath "count(//Skill)" "$f" 2>/dev/null)
        [[ "$count" -eq 42 ]] || return 1
    else
        echo "  (skipping xpath validation - xmllint not available)"
    fi
    grep -q 'base_attribute="Charisma"' "$f"
}

test_xml_party_templates() {
    echo "Testing: party templates XML"
    local f="$TEST_OUTPUT_DIR/ModuleData/party_templates/warband_party_templates.xml"
    [[ -f "$f" ]] || return 1
    if has_xmllint; then
        local count=$(xmllint --xpath "count(//MBPartyTemplate)" "$f" 2>/dev/null)
        [[ "$count" -eq 33 ]] || return 1
    else
        echo "  (skipping xpath validation - xmllint not available)"
    fi
    grep -q 'troop="NPCCharacter.wb_looter"' "$f"
}

test_xml_settlements() {
    echo "Testing: settlements XML"
    local f="$TEST_OUTPUT_DIR/ModuleData/settlements/warband_settlements.xml"
    [[ -f "$f" ]] || return 1
    if has_xmllint; then
        local count=$(xmllint --xpath "count(//Settlement)" "$f" 2>/dev/null)
        [[ "$count" -eq 148 ]] || return 1
    else
        echo "  (skipping xpath validation - xmllint not available)"
    fi
    grep -q 'is_castle="true"' "$f"
    grep -q 'is_castle="false"' "$f"
    grep -q '<Village' "$f"
}

test_quests_xml() {
    echo "Testing: quests XML"
    local f="$TEST_OUTPUT_DIR/ModuleData/quests/warband_quests.xml"
    [[ -f "$f" ]] || return 1
    if has_xmllint; then
        local count=$(xmllint --xpath "count(//Quest)" "$f" 2>/dev/null)
        [[ "$count" -eq 41 ]] || return 1
    else
        echo "  (skipping xpath validation - xmllint not available)"
    fi
    grep -q 'xp_reward="2"' "$f"
}

test_menus_xml() {
    echo "Testing: menus XML"
    local f="$TEST_OUTPUT_DIR/ModuleData/menus/warband_menus.xml"
    [[ -f "$f" ]] || return 1
    if has_xmllint; then
        local count=$(xmllint --xpath "count(//Menu)" "$f" 2>/dev/null)
        [[ "$count" -eq 183 ]] || return 1
    else
        echo "  (skipping xpath validation - xmllint not available)"
    fi
    grep -q "<Op code=" "$f"
}

test_dialog_xml() {
    echo "Testing: dialog states XML"
    local f="$TEST_OUTPUT_DIR/ModuleData/dialog_states/warband_dialog_states.xml"
    [[ -f "$f" ]] || return 1
    if has_xmllint; then
        local count=$(xmllint --xpath "count(//DialogState)" "$f" 2>/dev/null)
        [[ "$count" -eq 889 ]] || return 1
    else
        echo "  (skipping xpath validation - xmllint not available)"
    fi
    grep -q 'id="start"' "$f"
}

test_submodule() {
    echo "Testing: SubModule.xml"
    local f="$TEST_OUTPUT_DIR/SubModule.xml"
    [[ -f "$f" ]] || return 1
    grep -q 'id="Items" path="items"' "$f"
    grep -q 'id="Skills" path="skills"' "$f"
    grep -q 'id="PartyTemplates" path="party_templates"' "$f"
    grep -q 'id="Settlements" path="settlements"' "$f"
    grep -q 'id="Quests" path="quests"' "$f"
    grep -q 'id="Menus" path="menus"' "$f"
    grep -q 'id="DialogStates" path="dialog_states"' "$f"
}

test_module_info() {
    echo "Testing: ModuleInfo.xml"
    local f="$TEST_OUTPUT_DIR/ModuleInfo.xml"
    [[ -f "$f" ]] || return 1
    grep -q 'value="WarbandPort"' "$f"
    grep -q 'value="v1.0.0"' "$f"
    grep -q 'Id="Native"' "$f"
    grep -q 'Id="SandBoxCore"' "$f"
    grep -q 'Id="Sandbox"' "$f"
}

test_languages() {
    echo "Testing: languages"
    local f="$TEST_OUTPUT_DIR/Languages/en/strings.xml"
    [[ -f "$f" ]] || return 1
    local count
    if has_xmllint; then
        count=$(xmllint --xpath "count(//String)" "$f" 2>/dev/null)
    else
        count=$(grep -c '<String' "$f" 2>/dev/null || echo 0)
    fi
    [[ $count -gt 500 ]]
}

test_readme() {
    echo "Testing: README"
    local f="$TEST_OUTPUT_DIR/README.md"
    [[ -f "$f" ]] || return 1
    grep -q "items/weapons.xml" "$f"
    grep -q "NPC role templates" "$f"
}

test_report() {
    echo "Testing: report JSON"
    local f="$TEST_OUTPUT_DIR/conversion-report.json"
    [[ -f "$f" ]] || return 1
    local items=$(jq -r '.counts.itemsConverted' "$f" 2>/dev/null)
    [[ "$items" == "420" ]]
    local troops=$(jq -r '.counts.troopsConverted' "$f" 2>/dev/null)
    [[ "$troops" == "134" ]]
}

test_deterministic() {
    echo "Testing: deterministic output"
    run "$CLI_DLL" port-warband \
        --native "$NATIVE_DIR" --output /tmp/bp_det1 \
        --submodule --readme --party-templates --settlements --quests --menus --dialog-states >/dev/null 2>&1
    sync; sleep 0.5
    run "$CLI_DLL" port-warband \
        --native "$NATIVE_DIR" --output /tmp/bp_det2 \
        --submodule --readme --party-templates --settlements --quests --menus --dialog-states >/dev/null 2>&1
    sync; sleep 0.5
    local diff_output
    diff_output=$(diff -u /tmp/bp_det1/ModuleData/spnpccharacters.xml /tmp/bp_det2/ModuleData/spnpccharacters.xml | grep -v "GeneratedAtUtc" || true)
    if echo "$diff_output" | grep -q "^[-+]"; then
        echo "DIFFERENCES FOUND:"
        echo "$diff_output" | grep "^[-+]" | head -20
        return 1
    fi
}

test_install() {
    echo "Testing: install"
    local test_dir="/tmp/bp_install_test"
    rm -rf "$test_dir"
    mkdir -p "$test_dir/Modules"
    run "$CLI_DLL" port-warband \
        --native "$NATIVE_DIR" --output /tmp/bp_install_test \
        --install --bannerlord-dir "$test_dir" \
        --module-name WarbandPort \
        --submodule --readme --party-templates --settlements --quests --menus --dialog-states >/dev/null 2>&1
    [[ -d "/tmp/bp_install_test/Modules/WarbandPort" ]]
    [[ -f "/tmp/bp_install_test/Modules/WarbandPort/ModuleInfo.xml" ]]
    [[ -f "/tmp/bp_install_test/Modules/WarbandPort/SubModule.xml" ]]
    [[ -d "/tmp/bp_install_test/Modules/WarbandPort/ModuleData/items" ]]
}

test_cli_test() {
    echo "Testing: CLI test command"
    run "$CLI_DLL" test 2>&1 | grep -q "已通过! - 失败:     0，通过:    46"
}

# ========== Main ==========
run_test() {
    local name=$1
    shift
    echo -n "Testing: $name... "
    if "$@"; then
        echo "PASS"
        return 0
    else
        echo "FAIL"
        return 1
    fi
}

echo "===== WarbandPort 断点测试 ====="

# 先运行 setup
setup

tests=(
    test_build
    test_help
    test_version
    test_native
    test_items
    test_troops
    test_factions
    test_skills
    test_party_templates
    test_settlements
    test_quests
    test_menus
    test_dialog
    test_xml_items
    test_xml_npcs
    test_xml_cultures
    test_xml_skills
    test_xml_party_templates
    test_xml_settlements
    test_quests_xml
    test_menus_xml
    test_dialog_xml
    test_submodule
    test_module_info
    test_languages
    test_readme
    test_report
    test_deterministic
    test_install
    test_cli_test
)

passed=0
failed=0

for test in "${tests[@]}"; do
    echo -n "Testing: $test... "
    if "$test"; then
        echo "PASS"
        passed=$((passed + 1))
    else
        echo "FAIL"
        failed=$((failed + 1))
    fi
done

echo
echo "===== 结果: $passed 通过, $failed 失败 ====="
[[ $failed -eq 0 ]] || exit 1