// Types for Logistics (consignments, trips, fleet).
export type VehicleType = 'Pickup' | 'MiniTruck' | 'Truck' | 'Trailer' | 'Container20' | 'Container40' | 'Van' | 'Reefer'
export type VehicleStatus = 'Available' | 'OnTrip' | 'Maintenance' | 'Inactive'
export type ServiceLevel = 'PartLoad' | 'FullTruck' | 'Express'
export type PaymentMode = 'Prepaid' | 'ToPay' | 'Account'
export type ShipmentStatus = 'Booked' | 'PickedUp' | 'InTransit' | 'AtHub' | 'OutForDelivery' | 'Delivered' | 'Returned' | 'Cancelled'
export type TripStatus = 'Planned' | 'Dispatched' | 'Completed' | 'Cancelled'
export type TripExpenseType = 'Fuel' | 'Tolls' | 'DriverAllowance' | 'Loading' | 'VehicleHire' | 'Repairs' | 'Other'

export interface Vehicle {
  id: string; entityId: string; entityName: string; registrationNo: string; type: VehicleType; makeModel?: string; capacityKg: number; capacityCbm?: number
  isHired: boolean; ownerVendorId?: string; ownerVendorName?: string; fitnessExpiry?: string; insuranceExpiry?: string; routePermitExpiry?: string
  tokenTaxExpiry?: string; odometer: number; status: VehicleStatus; documentAlerts: string[]; currentTrip?: string
}
export interface Driver {
  id: string; entityId: string; fullName: string; cnic?: string; phone?: string; licenseNo: string; licenseCategory?: string; licenseExpiry?: string
  dailyAllowance: number; employeeId?: string; isActive: boolean; onTrip: boolean; licenseAlert?: string
}
export interface FreightRoute {
  id: string; code: string; origin: string; destination: string; distanceKm: number; standardHours: number; ratePerKg: number; minimumCharge: number
  fullTruckRate: number; fuelSurchargePercent: number; taxRateId?: string; isActive: boolean
}
export interface Maintenance { id: string; vehicleId: string; registrationNo: string; date: string; description: string; odometer?: number; cost: number; vendorName?: string; billId?: string; nextServiceDate?: string }
export interface Quote { freight: number; fuelSurcharge: number; otherCharges: number; subtotal: number; taxAmount: number; total: number; basis: string }
export interface ShipmentEvent { at: string; status: ShipmentStatus; location?: string; remarks?: string; byName?: string }
export interface Shipment {
  id: string; number: string; entityId: string; entityName: string; bookingDate: string; customerId: string; customerName: string
  shipperName: string; shipperPhone?: string; shipperAddress?: string; consigneeName: string; consigneePhone?: string; consigneeAddress?: string
  originCity: string; destinationCity: string; routeId?: string; routeCode?: string; service: ServiceLevel; goodsDescription?: string; pieces: number
  weightKg: number; volumeCbm?: number; declaredValue: number; paymentMode: PaymentMode; freight: number; fuelSurcharge: number; otherCharges: number
  taxAmount: number; total: number; codAmount: number; codCollected: number; codRemitted: boolean; status: ShipmentStatus; promisedDate?: string
  deliveredAt?: string; receivedBy?: string; deliveryRemarks?: string; invoiceId?: string; invoiceNumber?: string; tripNumber?: string; events: ShipmentEvent[]
}
export interface ShipmentListItem {
  id: string; number: string; bookingDate: string; customerName: string; consigneeName: string; originCity: string; destinationCity: string; pieces: number
  weightKg: number; paymentMode: PaymentMode; total: number; codAmount: number; status: ShipmentStatus; tripNumber?: string; late: boolean; invoiced: boolean
}
export interface TripExpense { id: string; type: TripExpenseType; description?: string; amount: number; paidFrom?: string; vendorName?: string; billId?: string; date: string }
export interface TripListItem { id: string; number: string; vehicleNo: string; driverName: string; origin: string; destination: string; plannedDate: string; status: TripStatus; shipments: number; loadKg: number; capacityKg: number; freight: number; expenses: number }
export interface Trip {
  id: string; number: string; entityId: string; vehicleId: string; vehicleNo: string; vehicleType: VehicleType; capacityKg: number; driverId: string
  driverName: string; driverPhone?: string; routeId?: string; routeCode?: string; origin: string; destination: string; plannedDate: string
  dispatchedAt?: string; arrivedAt?: string; odometerStart?: number; odometerEnd?: number; status: TripStatus; notes?: string
  shipments: ShipmentListItem[]; expenses: TripExpense[]; loadKg: number; freight: number; expensesTotal: number; margin: number
}
export interface ExpiryAlert { kind: string; subject: string; document: string; expiry?: string; daysLeft?: number }
export interface LogisticsDashboard {
  shipmentsByStatus: Record<string, number>; bookedToday: number; deliveredToday: number; onTimePercent: number; revenueThisMonth: number
  codPendingRemittance: number; fleetByStatus: Record<string, number>; alerts: ExpiryAlert[]; activeTrips: TripListItem[]
}

export const SHIPMENT_COLORS: Record<ShipmentStatus, string> = { Booked: 'gold', PickedUp: 'cyan', InTransit: 'blue', AtHub: 'geekblue', OutForDelivery: 'purple', Delivered: 'green', Returned: 'volcano', Cancelled: 'red' }
export const TRIP_COLORS: Record<TripStatus, string> = { Planned: 'gold', Dispatched: 'blue', Completed: 'green', Cancelled: 'red' }
export const VEHICLE_COLORS: Record<VehicleStatus, string> = { Available: 'green', OnTrip: 'blue', Maintenance: 'orange', Inactive: 'default' }
export const MODE_COLORS: Record<PaymentMode, string> = { Prepaid: 'green', ToPay: 'orange', Account: 'blue' }
export const VEHICLE_TYPES: VehicleType[] = ['Pickup', 'Van', 'MiniTruck', 'Truck', 'Trailer', 'Container20', 'Container40', 'Reefer']
export const EXPENSE_TYPES: TripExpenseType[] = ['Fuel', 'Tolls', 'DriverAllowance', 'Loading', 'VehicleHire', 'Repairs', 'Other']
export const SHIPMENT_STATUSES: ShipmentStatus[] = ['Booked', 'PickedUp', 'InTransit', 'AtHub', 'OutForDelivery', 'Delivered', 'Returned', 'Cancelled']
export const words = (s: string) => ({ ToPay: 'To pay', Container20: "20' container", Container40: "40' container", PartLoad: 'Part load', FullTruck: 'Full truck' } as Record<string, string>)[s]
  ?? s.replace(/([a-z])([A-Z])/g, '$1 $2')
export const kg = (n?: number) => `${(n ?? 0).toLocaleString('en-PK', { maximumFractionDigits: 1 })} kg`
