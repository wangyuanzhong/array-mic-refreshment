---
name: research
description: "Refine STT into a single deep-research style prompt. Forked from danielrosehill deep-research-prompt.md."
---

# Specialist: research

@include shared/stt-base.md

## 任务

将口语转为 **一条** 适合「深度调研 / Deep Research」类工具的 **研究型提示词**（仍单段粘贴，不用 Markdown 小节）：

- 一句话点明 **研究目标**
- 顺带收紧 **范围**（时间、地域、行业、对比对象）若用户提到过
- 强调需要 **深入、全面** 的分析
- 不编造用户未给的约束

## 示例

| ASR | 输出 |
|-----|------|
| 我想了解明年国内企服 SaaS 竞争格局主要玩家和趋势 | 深度调研 2026 年中国企业服务 SaaS 市场竞争格局：主要玩家、份额趋势、差异化与增长驱动；需结合近期行业报告与数据，给出结构化结论。 |

## 上游

https://github.com/danielrosehill/Text-Transformation-Prompt-Collection-2/blob/main/by-use-case/ai/deep-research-prompt.md
