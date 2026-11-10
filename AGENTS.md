# AGENTS.md — SpectroscopicAnalysis 上位机编码规则（强制）

本文件是 AI 编码助手在本仓库工作的**硬性规则**。详细依据见：
- 需求：[docs/上位机任务清单.md](docs/上位机任务清单.md)
- 规范：[docs/开发规范.md](docs/开发规范.md)

编码前必须先读上述两份文档；本文与其冲突时以 `docs/开发规范.md` 为准。

## 项目一句话

铝锭样品自动化光谱分析系统上位机：样品档案 + SCT 策略引擎 + 流程编排 + 校正计算 + 数据管理，通过 **Modbus TCP**（当前）驱动西门子 PLC，TCP 协议对接 GNR 光谱仪。

## 技术栈（不得擅自变更或新增依赖）

.NET 8 · WPF+MVVM（严格分层，见硬性规则 1）· CommunityToolkit.Mvvm（MVVM 基础设施，已登记）· HandyControl（UI 组件库，已登记）· LiveCharts2（图表库，已登记）· NModbus（Modbus TCP）· SQLite+Repository 抽象 · xUnit · Serilog · Generic Host DI。新依赖须先在开发规范第 1 节登记；UI 美化类插件（图表/图标/动画等）登记后可直接引入。

## 目录约定

- 业务代码放 `src/SpectroscopicAnalysis.Host/<模块>/`，模块：SampleManager / SctStrategy / Workflow / Correction / DeviceInterface / Vision / LoggingAlarm。
- 测试放 `tests/SpectroscopicAnalysis.Tests/`。
- 配置（寄存器映射表等）放 `config/`，与代码分离。

## 硬性规则

1. **架构**：依赖方向 UI → Workflow → 引擎/管理器 → DeviceInterface/数据层，禁止反向与跨层直调；模块间经接口通信。**MVVM 严格分层（强制）**：View 与 ViewModel 职责互斥、严禁越职——code-behind 仅限 InitializeComponent 与纯视觉行为，**禁止**业务逻辑/直接调用任何模块/事件承载业务（用户操作一律 `ICommand`）；ViewModel **禁止**引用 WPF 类型（Brush/Color/Window/Control/VisualTreeHelper 等）、禁止持有控件引用与操作视觉树（跨线程经注入的 `IUiDispatcher`），**禁止**在 VM 内实现持久化或通信。细则见开发规范 3.3。
2. **DeviceInterface 必须抽象**：Modbus TCP 为当前实现，接口设计须保证后续可替换为 OPC UA（不要把 Modbus 细节泄漏到接口之外）。
3. **数据**：原始数据与校正后数据必须双存；系数版本化；写库用事务；审计日志完整（前后值/时间/原因/来源样品；不含操作者——身份内容已移除）。
4. **状态机枚举命名**必须与任务清单第 7 节一致（系统模式/任务状态/SCT 子状态/样品状态），不得自创状态名。
5. **错误处理**：
   - 禁止空 catch / 吞异常；异常必须走统一错误链（LoggingAlarm），携带错误码 + TaskID/SampleID/步骤上下文。
   - 错误码必须引用统一错误码表常量（开发规范 11.1），禁止写数字 magic number；新错误码先登记再使用。
   - 所有设备通信调用必须带超时与重试上限（值取集中配置）。
   - 任务下发 PLC 前必须预检 SafetyOK / RobotReady / SparkStandReady。
6. **编码**：可空引用启用；中文注释与日志；async I/O 禁止 `.Result/.Wait()`；file-scoped namespace。
7. **UI**：必须遵循 [docs/界面设计规范.md](docs/界面设计规范.md)——颜色/字号/样式一律引用 `Themes/` 资源字典（`Brush.*`/`Color.*` 键），禁止硬编码颜色与内联样式；ViewModel 禁止返回 Brush/Color；状态色语义固定不得挪用。**弹性布局强制**：容器 Grid+Star 比例，禁止对内容容器写固定宽高（图标白名单除外，见规范第 5 节）；字号一律引用 Typography 资源（禁止直接写 FontSize 数值），窗口支持 100/125/150% 缩放档位（根容器 LayoutTransform）。**原型已冻结**：`docs/prototypes/` 仅作初始设计参照，页面修改不同步原型文件，以程序界面为准。
8. **安全**：密钥/连接串不入库；样品位置与软件记录不一致时不得切入自动模式。
9. **Git**：conventional commits 中文描述；提交前 `dotnet build` 必须通过。
10. **范围**：仅修改与当前任务相关的代码；不重构无关代码；不删除现有规则文档中的约束。
11. **禁止身份内容**：程序不得包含用户账号、角色权限、操作员/登录展示、操作者字段等任何身份相关操作与内容（开发规范第 5 节决策），新增功能亦不得引入。
