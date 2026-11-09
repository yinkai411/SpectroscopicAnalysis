# AGENTS.md — SpectroscopicAnalysis 上位机编码规则（强制）

本文件是 AI 编码助手在本仓库工作的**硬性规则**。详细依据见：
- 需求：[docs/上位机任务清单.md](docs/上位机任务清单.md)
- 规范：[docs/开发规范.md](docs/开发规范.md)

编码前必须先读上述两份文档；本文与其冲突时以 `docs/开发规范.md` 为准。

## 项目一句话

铝锭样品自动化光谱分析系统上位机：样品档案 + SCT 策略引擎 + 流程编排 + 校正计算 + 数据管理，通过 **Modbus TCP**（当前）驱动西门子 PLC，TCP 协议对接 GNR 光谱仪。

## 技术栈（不得擅自变更或新增依赖）

.NET 8 · WPF+MVVM · NModbus（Modbus TCP）· SQLite+Repository 抽象 · xUnit · Serilog · Generic Host DI。新依赖须先在开发规范第 1 节登记。

## 目录约定

- 业务代码放 `src/SpectroscopicAnalysis.Host/<模块>/`，模块：SampleManager / SctStrategy / Workflow / Correction / DeviceInterface / Vision / LoggingAlarm。
- 测试放 `tests/SpectroscopicAnalysis.Tests/`。
- 配置（寄存器映射表等）放 `config/`，与代码分离。

## 硬性规则

1. **架构**：依赖方向 UI → Workflow → 引擎/管理器 → DeviceInterface/数据层，禁止反向与跨层直调；模块间经接口通信。
2. **DeviceInterface 必须抽象**：Modbus TCP 为当前实现，接口设计须保证后续可替换为 OPC UA（不要把 Modbus 细节泄漏到接口之外）。
3. **数据**：原始数据与校正后数据必须双存；系数版本化；写库用事务；审计日志完整（前后值/时间/原因/操作者）。
4. **状态机枚举命名**必须与任务清单第 7 节一致（系统模式/任务状态/SCT 子状态/样品状态），不得自创状态名。
5. **错误处理**：
   - 禁止空 catch / 吞异常；异常必须走统一错误链（LoggingAlarm），携带错误码 + TaskID/SampleID/步骤上下文。
   - 错误码必须引用统一错误码表常量（开发规范 11.1），禁止写数字 magic number；新错误码先登记再使用。
   - 所有设备通信调用必须带超时与重试上限（值取集中配置）。
   - 任务下发 PLC 前必须预检 SafetyOK / RobotReady / SparkStandReady。
6. **编码**：可空引用启用；中文注释与日志；async I/O 禁止 `.Result/.Wait()`；file-scoped namespace。
7. **安全**：密钥/连接串不入库；样品位置与软件记录不一致时不得切入自动模式。
8. **Git**：conventional commits 中文描述；提交前 `dotnet build` 必须通过。
9. **范围**：仅修改与当前任务相关的代码；不重构无关代码；不删除现有规则文档中的约束。
