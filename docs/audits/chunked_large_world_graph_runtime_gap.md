# Chunked Large-World Graph Runtime Gap Audit

Date: 2026-03-21
Reporter: Codex
Scope: Ludots Core Navigation / Map Runtime

## 1. Summary

Ludots already contains most of the generic infrastructure needed for chunked large-world navigation:

- chunk-based spatial partitioning
- `ILoadedChunks` as a loaded-area lifecycle contract
- AOI-driven chunk visibility
- chunked node-graph storage
- a loaded-view flattener
- a reusable hierarchical graph pathing primitive

The missing part is the final generic runtime glue between these pieces. Today the graph pathing bootstrap still builds a one-off loaded snapshot from `ChunkedNodeGraphStore`, while `NodeGraphBoard` does not expose a loaded-chunk source and `AutoPathService` still defaults to a linear-scan projection index.

For large-world chunked maps, this leaves Ludots with reusable pieces but without a stable, map-scoped graph runtime that can keep graph loading, projection, and pathing in sync with chunk lifecycle.

## 2. Existing Generic Infrastructure

The following generic building blocks already exist in Ludots:

- `src/Core/Spatial/ILoadedChunks.cs`
- `src/Core/Navigation/AOI/HexGridAOI.cs`
- `src/Core/Spatial/ChunkedGridSpatialPartitionWorld.cs`
- `src/Core/Spatial/SpatialQueryService.cs`
- `src/Core/Navigation/GraphWorld/ChunkedNodeGraphStore.cs`
- `src/Core/Navigation/GraphWorld/LoadedGraphView.cs`
- `src/Core/Navigation/MultiLayerGraph/HierarchicalPathService.cs`
- `src/Core/Map/Hex/VertexMap.cs`

This is enough to support a generic large-world graph foundation, but not yet enough to run it efficiently as a first-class board/runtime service.

## 3. Infrastructure Gaps

### 3.1 No map-scoped loaded-graph runtime

`NodeGraphBoard` owns a `ChunkedNodeGraphStore`, but it does not expose `LoadedChunks`, so there is no board-level lifecycle bridge for graph chunk load/unload.

Evidence:

- `src/Core/Map/Board/NodeGraphBoard.cs`

### 3.2 Pathing bootstrap still uses one-off flattening

`GameEngine.BuildPathingGraph()` currently calls `nodeGraphBoard.GraphStore.BuildLoadedView().Graph` directly. This produces a snapshot graph, but not a persistent runtime view with dirty tracking, cache ownership, or rebuild policy tied to chunk lifecycle.

Evidence:

- `src/Core/Engine/GameEngine.cs`
- `src/Core/Navigation/GraphWorld/ChunkedNodeGraphStore.cs`
- `src/Core/Navigation/GraphWorld/LoadedGraphView.cs`

### 3.3 Default graph projection is not chunk-scale friendly

`AutoPathService` still constructs `LinearScanNodeGraphSpatialIndex` by default even though `UniformGridNodeGraphSpatialIndex` already exists. That is workable for small graphs, but it is not an appropriate default for large loaded graph views.

Evidence:

- `src/Core/Navigation/Pathing/AutoPathService.cs`
- `src/Core/Navigation/GraphCore/LinearScanNodeGraphSpatialIndex.cs`
- `src/Core/Navigation/GraphCore/UniformGridNodeGraphSpatialIndex.cs`

### 3.4 Hierarchical pathing exists but lacks a standard runtime entry

`HierarchicalPathService` exists as a reusable primitive, but Ludots still lacks a standard builder/runtime path for chunked strategic graphs that want coarse/fine routing as part of normal map services.

Evidence:

- `src/Core/Navigation/MultiLayerGraph/HierarchicalPathService.cs`

## 4. Requested Generic Follow-up

Suggested infrastructure follow-up:

1. Add a map-scoped loaded-graph runtime service that owns cached `LoadedGraphView` state and rebuild policy.
2. Bridge `NodeGraphBoard` graph lifecycle to `ILoadedChunks` so chunk load/unload can invalidate or refresh the graph runtime explicitly.
3. Replace linear-scan graph projection as the production default for large loaded graphs.
4. Provide a standard builder/runtime entry for hierarchical chunked graph pathing.

## 5. Boundary

This report is intentionally limited to generic Ludots infrastructure gaps. It does not request any game-specific road rules, rendering behavior, campaign semantics, or product-layer authoring policy.

