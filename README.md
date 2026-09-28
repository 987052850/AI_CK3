# AI_CK3 Mod

## 简介 / Introduction

**AI_CK3** 是一个为《骑士王国3》（Crusader Kings III）设计的游戏模组，旨在通过强化 AI 决策逻辑，提升游戏体验和挑战性。

**AI_CK3** is a mod for Crusader Kings III focused on enhancing AI decision-making logic, making gameplay more challenging and immersive.

## 功能 / Features

- 改进 AI 势力的政治与战争决策 / Improved AI political and warfare decisions
- 新增 AI 专属的脚本触发条件与效果 / New scripted triggers and effects for AI behavior
- 增强 AI 外交与联盟策略 / Enhanced AI diplomacy and alliance strategies

## 安装 / Installation

1. 将 `mod/AI_CK3` 文件夹复制到你的 CK3 mod 目录  
   Copy the `mod/AI_CK3` folder to your CK3 mod directory:
   - Windows: `%USERPROFILE%\Documents\Paradox Interactive\Crusader Kings III\mod\`
   - Linux: `~/.local/share/Paradox Interactive/Crusader Kings III/mod/`

2. 将 `descriptor.mod` 文件复制到同一目录，并重命名为 `AI_CK3.mod`  
   Copy `descriptor.mod` to the same directory and rename it to `AI_CK3.mod`.  
   _(Or copy the provided `AI_CK3.mod` file directly — it is identical to `descriptor.mod`.)_

3. 在游戏启动器中启用 AI_CK3 模组  
   Enable the AI_CK3 mod in the game launcher.

## 兼容性 / Compatibility

- CK3 版本 / CK3 Version: 1.12.*
- 与大多数纯脚本模组兼容 / Compatible with most script-only mods

## 目录结构 / Directory Structure

```
AI_CK3/                              ← repository root
├── descriptor.mod                   ← copy to CK3 mod folder
├── AI_CK3.mod                       ← copy to CK3 mod folder
├── README.md
└── mod/
    └── AI_CK3/                      ← copy entire folder to CK3 mod folder
        ├── common/
        │   ├── scripted_triggers/   # AI 触发条件
        │   ├── scripted_effects/    # AI 效果脚本
        │   ├── decisions/           # AI 决策
        │   ├── modifiers/           # 角色修正
        │   ├── opinion_modifiers/   # 外交意见修正
        │   └── on_actions/          # 事件钩子
        ├── events/                  # 事件文件
        ├── localization/            # 本地化文本
        │   ├── english/
        │   └── simp_chinese/
        └── gfx/                     # 图形资源
            └── interface/
```

## 许可证 / License

MIT License