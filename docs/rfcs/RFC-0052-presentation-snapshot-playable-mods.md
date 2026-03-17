# RFC-0052 表现层 snapshot playable mod 设计

本文是 `PresentationVisualSnapshotBuffer` 后续验收的 playable mod 提案。它不定义正式规范，只给出可被产品用户直接观察的验证场景，用于检验 adapter 侧 persistent skeleton manager、persistent static manager 与 dirty sync 是否正确消费 Core frame snapshot。

## 1 目标与非目标

目标：

* 用真实玩家动作覆盖 off-screen、hide、reappear、despawn、reuse 场景。
* 同时验证 skinned mesh lane 与 static mesh lane。
* 让 Unity / Unreal adapter 的失败模式可以被产品用户直接看到，而不是只体现在调试日志里。

非目标：

* 不定义 wire protocol。
* 不替代 `docs/architecture/presentation_snapshot_contract.md` 的 Core contract 说明。
* 不要求在同一个 issue 内完成 3 个 mod 的全部实现。

## 2 Mod A：Skinned Crowd Arena

### 2.1 玩家动作

1. 玩家进入圆形竞技场，选中 20 到 50 个 skinned 英雄或单位。
2. 玩家持续下发移动、转向、追击和撤退命令。
3. 玩家拖拽相机离开战团，让主战团离屏 3 到 5 秒。
4. 玩家把相机拉回战团，继续观察同一批单位。
5. 玩家触发 reinforcements 生成与阵亡单位回收。

### 2.2 关键场景

* off-screen：单位离屏后继续移动、转向、切换动画状态。
* hide：单位进入潜行、烟雾或建筑遮挡状态，adapter 侧需要保留 skeleton 实例身份但切换可见性。
* reappear：单位重新出现在视野内时，位置、朝向、动画时间都应连续。
* despawn：单位死亡或撤离后应移除对应 skeleton 实例。
* reuse：新一波 reinforcements 不能复用旧 `StableId`，也不能把旧骨架状态串到新单位。

### 2.3 Unity / Unreal 预期行为

* skeleton manager：按 `StableId` 维护 skinned actor；离屏期间继续消费 transform / animator 更新，重新入屏时不得 snap back、错朝向或串动画。
* static manager：竞技场地面、障碍和看台等 static props 保持稳定，不因 skinned lane churn 触发无关实例重建。

### 2.4 产品可见通过标准

* 玩家把镜头移回战团时，看不到任何单位瞬移回旧位置。
* 同一个英雄离屏前后的朝向和动画片段连续，没有 A 单位变成 B 单位的现象。
* 增援单位出现时不会继承阵亡单位的旧骨架姿态。

### 2.5 产品可见失败标准

* 重新入屏时单位先闪到旧位置再跳回当前帧。
* 单位朝向错误，面朝旧方向开火或移动。
* 新生成单位出现时套用了旧单位的动画时间或武器姿态。

## 3 Mod B：Static World Churn

### 3.1 玩家动作

1. 玩家进入资源据点地图，镜头在多个采集区和建筑区之间切换。
2. 玩家建造、取消、拆除、升级建筑。
3. 玩家采集资源、耗尽资源点、等待资源点刷新。
4. 玩家反复把相机移出并移回同一区域。

### 3.2 关键场景

* off-screen：建筑、资源点和装饰 props 离屏期间继续发生建造进度、损坏与替换。
* hide：占位建筑、施工脚手架、地块装饰按游戏规则进入 hidden / culled。
* reappear：玩家重新看到同一区域时，实例数量、模型、朝向和缩放与当前世界状态一致。
* despawn：被拆除的建筑和被采空的资源点应立刻释放实例。
* reuse：新建筑或刷新资源点即使复用了逻辑槽位，也必须拿到新的持久实例身份映射。

### 3.3 Unity / Unreal 预期行为

* static manager：按 `StableId` 维护 instanced static mesh 或 actor 实例；隐藏只切换可见性，despawn 才释放实例槽。
* skeleton manager：本场景应基本空闲，只保留可能存在的工人或旗手等极少数 skinned actor，不得因为 static churn 触发额外抖动。

### 3.4 产品可见通过标准

* 玩家切回区域时，看不到旧建筑残影或已拆除资源点“复活一帧”。
* 同一建筑升级后模型切换正确，不会复制出第二栋或遗留旧实例。
* 资源点刷新后表现为新的实例，而不是旧实例残留旧损坏状态。

### 3.5 产品可见失败标准

* 相机拉回时先看到已删除建筑，再下一帧消失。
* 建筑升级后旧模型和新模型重叠。
* 资源点刷新时继承上一次采空后的破损表现或错误缩放。

## 4 Mod C：Hybrid Battlefield

### 4.1 玩家动作

1. 玩家进入混合战场，场内同时存在 skinned 英雄、小兵、instanced static 掩体、资源物件、marker 和效果。
2. 玩家切换多个战线，交替观察前线英雄、后方建筑与大范围技能区域。
3. 玩家连续触发召唤、阵亡、建造、爆炸和标记类技能。
4. 玩家快速拖拽镜头跨越多个热点区域，再返回主战场。

### 4.2 关键场景

* off-screen：前线英雄离屏时继续移动、转向和播动画；后方 static props 同时发生建造与破坏。
* hide：隐身英雄、被遮挡建筑、临时 marker、一次性效果分别进入各自隐藏状态。
* reappear：镜头回到主战场时，英雄、掩体、资源点和技能标记都应与当前世界一致。
* despawn：阵亡英雄、爆炸后消失的 props、过期 marker 与 effects 都应被正确回收。
* reuse：复活英雄、重建掩体和再次施放的技能标记必须使用各自新的生命周期语义，不能串上旧实例状态。

### 4.3 Unity / Unreal 预期行为

* skeleton manager：仅维护 skinned hero / unit，按 `StableId` 保持离屏期间的 transform 与 animator 连续性。
* static manager：维护 instanced static props、建筑和资源物件，按 `Visibility` 与生命周期处理 hide / despawn / reuse。
* marker / effect manager：一次性或短生命周期表现项可使用独立 manager，但不得污染 skeleton / static 两条主 lane 的身份映射。

### 4.4 产品可见通过标准

* 镜头跨区切换后，主战场回看没有英雄瞬移、建筑残影和 marker 复活。
* 同一时刻既能看到英雄动作连续，也能看到 static props 的生成/销毁正确。
* 大范围技能结束后，过期 marker 与效果全部清理，不影响后续再次施放。

### 4.5 产品可见失败标准

* 回到主战场时英雄动画和位置正确，但 static props 遗留旧实例。
* 或者 static props 正常，skinned 英雄却丢失姿态、朝向或身份映射。
* marker / effect 过期后残留在场景中，或再次施放时借用了旧实例。

## 5 建议验收产物

每个 playable mod 在真正实现时，建议至少产出以下证据：

* 头less trace：记录玩家动作、关键实体 `StableId`、`Visibility` 与 transform 变化。
* battle-report：把玩家动作和产品可见结果写成按时间排序的可读报告。
* path artifact：覆盖 happy path、off-screen path、despawn path 与 reuse path。

## 6 相关文档

* 当前 Core contract：见 [../architecture/presentation_snapshot_contract.md](../architecture/presentation_snapshot_contract.md)
* 文档总览：见 [../README.md](../README.md)
