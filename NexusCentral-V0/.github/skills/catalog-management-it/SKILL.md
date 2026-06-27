---
name: catalog-management-it
description: "Use when adding, importing, validating, or querying catalog entries for models, workflows, wrappers, or templates."
---

# Catalog Management It

## When to Use
- Add or update catalog entries.
- Work on import, reload, validation, or lookup behavior.
- Keep catalog state consistent across disk and memory.

## Procedure
1. Follow the existing load, clone, and persist pattern.
2. Keep mutations behind service methods and raise change events.
3. Store state under AppPathsService-managed directories.

## References
- [ModelCatalogService.cs](../../../Desktop/Services/ModelCatalogService.cs)
- [WorkflowCatalogService.cs](../../../Desktop/Services/WorkflowCatalogService.cs)
- [WrapperCatalogService.cs](../../../Desktop/Services/WrapperCatalogService.cs)
- [TemplateCatalogService.cs](../../../Desktop/Services/TemplateCatalogService.cs)
- [ModelCatalogEntry.cs](../../../Desktop/Models/ModelCatalogEntry.cs)
