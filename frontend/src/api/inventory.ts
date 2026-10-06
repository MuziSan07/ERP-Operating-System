// Types for Inventory & Procurement.
export type ItemType = 'Stock' | 'Service'
export type StockTransactionType = 'Issue' | 'Transfer' | 'Adjustment' | 'Opening'
export type StockMovementType = 'Opening' | 'Receipt' | 'Issue' | 'TransferOut' | 'TransferIn' | 'AdjustmentIn' | 'AdjustmentOut'
export type PurchaseRequestStatus = 'Draft' | 'Submitted' | 'Approved' | 'Rejected' | 'Ordered' | 'Cancelled'
export type PurchaseOrderStatus = 'Draft' | 'Approved' | 'PartiallyReceived' | 'Received' | 'Closed' | 'Cancelled'

export interface ItemCategory { id: string; code: string; name: string }
export interface Item {
  id: string; code: string; name: string; categoryId?: string; categoryName?: string; type: ItemType; unit: string; barcode?: string
  trackBatches: boolean; trackExpiry: boolean; reorderLevel: number; standardCost?: number; inventoryAccountId?: string
  consumptionAccountId?: string; purchaseTaxRateId?: string; isActive: boolean; onHand: number; value: number
}
export interface Warehouse { id: string; entityId: string; entityName: string; code: string; name: string; address?: string; isActive: boolean; stockValue: number }
export interface StockRow { itemId: string; itemCode: string; itemName: string; unit: string; category?: string; warehouseId: string; warehouseName: string; quantity: number; averageCost: number; value: number; reorderLevel: number; belowReorder: boolean }
export interface BatchRow { itemId: string; itemCode: string; itemName: string; warehouseId: string; warehouseName: string; batchId: string; batchNo: string; expiryDate?: string; daysToExpiry?: number; quantity: number }
export interface MovementRow { date: string; type: StockMovementType; itemCode: string; itemName: string; warehouseName: string; batchNo?: string; quantity: number; unitCost: number; value: number; quantityAfter: number; averageCostAfter: number; reference?: string; sourceId: string }
export interface Valuation { currency: string; stockValue: number; ledgerValue: number; difference: number; rows: StockRow[] }
export interface StockTransaction {
  id: string; type: StockTransactionType; number: string; date: string; warehouseId: string; warehouseName: string; toWarehouseId?: string
  toWarehouseName?: string; accountName?: string; reference?: string; notes?: string; totalValue: number; journalEntryId?: string; createdByName?: string
  lines: { itemId: string; itemCode: string; itemName: string; unit: string; batchNo?: string; expiryDate?: string; quantity: number; unitCost: number; value: number; notes?: string }[]
}
export interface PurchaseRequest {
  id: string; number: string; entityId: string; entityName: string; date: string; requiredBy?: string; purpose: string; status: PurchaseRequestStatus
  requestedByName?: string; approvedByName?: string; approvedAt?: string; decisionComment?: string; estimatedTotal: number
  lines: { id: string; itemId: string; itemCode: string; itemName: string; unit: string; quantity: number; estimatedUnitPrice: number; quantityOrdered: number; notes?: string }[]
}
export interface PoLine {
  id: string; itemId: string; itemCode: string; itemName: string; unit: string; itemType: ItemType; trackBatches: boolean; trackExpiry: boolean
  description: string; quantity: number; unitPrice: number; taxRateId?: string; taxRateName?: string; amount: number; taxAmount: number
  quantityReceived: number; quantityBilled: number; purchaseRequestLineId?: string
}
export interface LinkedDoc { id: string; number?: string; date: string; value: number; status: string }
export interface PurchaseOrder {
  id: string; number: string; entityId: string; entityName: string; vendorId: string; vendorName: string; vendorNtn?: string; vendorStrn?: string
  vendorAddress?: string; date: string; expectedDate?: string; warehouseId: string; warehouseName: string; currency: string; exchangeRate: number
  terms?: string; notes?: string; status: PurchaseOrderStatus; subtotal: number; taxTotal: number; total: number; createdByName?: string
  approvedByName?: string; approvedAt?: string; lines: PoLine[]; receipts: LinkedDoc[]; bills: LinkedDoc[]
}
export interface PurchaseOrderListItem { id: string; number: string; entityName: string; vendorName: string; date: string; expectedDate?: string; warehouseName: string; currency: string; status: PurchaseOrderStatus; total: number; receivedPercent: number; billedPercent: number }
export interface GoodsReceipt {
  id: string; number: string; purchaseOrderId: string; purchaseOrderNumber: string; vendorName: string; warehouseId: string; warehouseName: string
  date: string; deliveryNote?: string; notes?: string; totalValue: number; journalEntryId?: string; createdByName?: string
  lines: { itemId: string; itemCode: string; itemName: string; unit: string; quantity: number; batchNo?: string; expiryDate?: string; unitCost: number; value: number }[]
}
export interface GrniRow { purchaseOrderId: string; purchaseOrderNumber: string; vendorName: string; itemCode: string; itemName: string; quantityReceived: number; quantityBilled: number; unbilledValue: number }

export const PR_COLORS: Record<PurchaseRequestStatus, string> = { Draft: 'default', Submitted: 'gold', Approved: 'green', Rejected: 'red', Ordered: 'blue', Cancelled: 'default' }
export const PO_COLORS: Record<PurchaseOrderStatus, string> = { Draft: 'default', Approved: 'blue', PartiallyReceived: 'gold', Received: 'cyan', Closed: 'green', Cancelled: 'red' }
export const MOVE_LABEL: Record<StockMovementType, string> = { Opening: 'Opening', Receipt: 'Received', Issue: 'Issued', TransferOut: 'Transfer out', TransferIn: 'Transfer in', AdjustmentIn: 'Adjustment +', AdjustmentOut: 'Adjustment −' }
export const qty = (n?: number) => (n ?? 0).toLocaleString('en-PK', { maximumFractionDigits: 4 })
export const spaced = (s: string) => s.replace(/([a-z])([A-Z])/g, '$1 $2')
