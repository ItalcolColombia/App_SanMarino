# Tracker — Fix WAF bloquea rutas `/admin` en Tickets (403 transferencia)

Plan: [fix_waf_tickets_admin_route_plan.md](./fase_de_desarrollo/fix_waf_tickets_admin_route_plan.md)

## Estado

- [x] Diagnóstico: 403 viene de AWS WAF (`AWSManagedRulesAdminProtectionRuleSet`), no de la app
- [x] Confirmar rutas afectadas en código (back + front)
- [x] Plan + tracker
- [x] Backend: renombrar `admin` → `global` en `TicketsController.cs` (L225, L241)
- [x] Frontend: renombrar `/admin` → `/global` en `ticket.service.ts` (L122, L126)
- [x] Validar `dotnet build` (backend) → compila 0 errores (a output temporal, el bin estaba lockeado por el dev server)
- [x] Validar `yarn build` (frontend) → OK
- [x] Empresas: renombrar `admin` → `global` (`CompanyController.cs` + `company.service.ts` + etiquetas test) — confirmado por el usuario
- [x] Revalidar builds tras cambio de empresas → backend 0 errores, `yarn build` OK
- [ ] Deploy (esperar OK explícito del usuario)
- [ ] Verificación post-deploy: ECS TaskDef/imagen + 200 en `/api/tickets/global*` y `/api/Company/global`
