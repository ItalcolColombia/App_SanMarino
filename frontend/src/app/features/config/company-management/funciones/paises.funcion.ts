import { PaisesDiff } from '../models/company-management.model';

export function diffPaises(
  companyId: number,
  currentPaisIds: number[],
  selectedPaisIds: number[]
): PaisesDiff {
  const toRemove = currentPaisIds
    .filter(id => !selectedPaisIds.includes(id))
    .map(paisId => ({ companyId, paisId }));

  const toAdd = selectedPaisIds
    .filter(id => !currentPaisIds.includes(id))
    .map(paisId => ({ companyId, paisId }));

  return { toAdd, toRemove };
}

export function addPaisesOps(companyId: number, paisIds: number[]): { companyId: number; paisId: number }[] {
  return paisIds.map(paisId => ({ companyId, paisId }));
}
