// Types for the Hotel module.
export type HousekeepingStatus = 'Clean' | 'Dirty' | 'Inspected' | 'OutOfOrder'
export type BookingSource = 'WalkIn' | 'Phone' | 'Email' | 'Website' | 'Ota' | 'Corporate' | 'TravelAgent'
export type ReservationStatus = 'Tentative' | 'Confirmed' | 'CheckedIn' | 'CheckedOut' | 'Cancelled' | 'NoShow'
export type FolioChargeType = 'Room' | 'Restaurant' | 'Laundry' | 'Minibar' | 'Transport' | 'Other'

export interface RoomType { id: string; entityId: string; entityName: string; code: string; name: string; baseRate: number; maxAdults: number; maxChildren: number; description?: string; taxRateId?: string; incomeAccountId?: string; isActive: boolean; roomCount: number }
export interface Room { id: string; entityId: string; number: string; roomTypeId: string; roomTypeName: string; floor?: string; housekeeping: HousekeepingStatus; housekeepingNote?: string; isActive: boolean; occupied: boolean; guestName?: string; reservationId?: string; departureDate?: string }
export interface Guest { id: string; fullName: string; phone?: string; email?: string; cnic?: string; passportNo?: string; nationality?: string; address?: string; city?: string; isVip: boolean; notes?: string; stays: number }
export interface ReservationRoom { id: string; roomTypeId: string; roomTypeName: string; roomId?: string; roomNumber?: string; rate: number }
export interface FolioCharge { id: string; date: string; type: FolioChargeType; description: string; quantity: number; unitPrice: number; amount: number; taxName?: string; taxAmount: number; isVoid: boolean; fromStock: boolean }
export interface Reservation {
  id: string; number: string; entityId: string; entityName: string; guestId: string; guest: Guest; billToContactId?: string; billToName?: string
  source: BookingSource; arrivalDate: string; departureDate: string; nights: number; adults: number; children: number; status: ReservationStatus
  externalReference?: string; notes?: string; checkedInAt?: string; checkedOutAt?: string; invoiceId?: string; invoiceNumber?: string
  rooms: ReservationRoom[]; charges: FolioCharge[]; deposits: { id: string; date: string; amount: number; bankAccountName: string; reference?: string }[]
  chargesTotal: number; taxTotal: number; depositsTotal: number; balance: number; estimatedStayTotal: number
}
export interface ReservationListItem { id: string; number: string; guestName: string; phone?: string; isVip: boolean; entityName: string; arrivalDate: string; departureDate: string; nights: number; rooms: string; source: BookingSource; status: ReservationStatus; balance: number }
export interface Availability { from: string; to: string; nights: string[]; roomTypes: { roomTypeId: string; roomTypeName: string; baseRate: number; totalRooms: number; availableByNight: number[]; minAvailable: number }[] }
export interface FrontDesk { date: string; totalRooms: number; occupied: number; vacantClean: number; vacantDirty: number; outOfOrder: number; occupancyPercent: number; arrivals: ReservationListItem[]; departures: ReservationListItem[]; inHouse: ReservationListItem[] }
export interface TapeBlock { reservationId: string; number: string; guestName: string; from: string; to: string; status: ReservationStatus; isVip: boolean }
export interface TapeChart { from: string; days: number; rooms: { roomId: string; number: string; roomTypeName: string; housekeeping: HousekeepingStatus; blocks: TapeBlock[] }[]; unassigned: TapeBlock[] }
export interface HotelReport { from: string; to: string; days: { date: string; available: number; sold: number; occupancy: number; roomRevenue: number; adr: number; revPar: number }[]; available: number; sold: number; occupancy: number; roomRevenue: number; adr: number; revPar: number; revenueByType: Record<string, number> }

export const RES_COLORS: Record<ReservationStatus, string> = { Tentative: 'gold', Confirmed: 'blue', CheckedIn: 'green', CheckedOut: 'default', Cancelled: 'red', NoShow: 'volcano' }
export const HK_COLORS: Record<HousekeepingStatus, string> = { Clean: 'green', Inspected: 'cyan', Dirty: 'orange', OutOfOrder: 'red' }
export const SOURCES: BookingSource[] = ['WalkIn', 'Phone', 'Email', 'Website', 'Ota', 'Corporate', 'TravelAgent']
export const label = (s: string) => s === 'Ota' ? 'OTA' : s.replace(/([a-z])([A-Z])/g, '$1 $2')
/** Pakistan "today" — matches the API's front-desk date. */
export const pkToday = () => new Date(Date.now() + 5 * 3600_000).toISOString().slice(0, 10)
