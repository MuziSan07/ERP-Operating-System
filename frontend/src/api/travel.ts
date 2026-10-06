// Types for Travel & Tours.
export type DepartureStatus = 'Open' | 'Closed' | 'Departed' | 'Completed' | 'Cancelled'
export type TravelBookingStatus = 'Quotation' | 'Confirmed' | 'Invoiced' | 'Cancelled'
export type PassengerType = 'Adult' | 'Child' | 'Infant'
export type BookingItemType = 'TourSeats' | 'Flight' | 'Hotel' | 'Visa' | 'Transport' | 'Insurance' | 'Other'
export type VisaStatus = 'NotStarted' | 'DocumentsCollected' | 'Submitted' | 'Approved' | 'Rejected'

export interface ItineraryDay { dayNumber: number; title: string; description?: string; overnight?: string; meals?: string }
export interface TourPackage {
  id: string; entityId: string; entityName: string; code: string; name: string; destination: string; durationDays: number; summary?: string
  inclusions?: string; exclusions?: string; adultPrice: number; childPrice: number; singleSupplement: number; taxRateId?: string
  incomeAccountId?: string; isActive: boolean; upcomingDepartures: number; itinerary: ItineraryDay[]
}
export interface Guide { id: string; fullName: string; phone?: string; languages?: string; licenseNo?: string; dailyRate: number; employeeId?: string; isActive: boolean; upcomingDepartures: string[] }
export interface DepartureListItem { id: string; code: string; tourPackageId: string; packageName: string; destination: string; startDate: string; endDate: string; capacity: number; booked: number; seatsLeft: number; loadFactor: number; adultPrice: number; status: DepartureStatus; guides: string }
export interface Departure {
  id: string; entityId: string; code: string; tourPackageId: string; packageName: string; destination: string; startDate: string; endDate: string
  capacity: number; booked: number; seatsLeft: number; adultPrice: number; childPrice: number; status: DepartureStatus; notes?: string
  guides: { id: string; guideId: string; guideName: string; phone?: string; role?: string; dailyRate: number }[]
  costs: { id: string; description: string; vendorId?: string; vendorName?: string; amount: number; billId?: string; billNumber?: string }[]
  revenue: number; totalCost: number; margin: number
}
export interface ManifestRow { bookingNumber: string; customerName: string; passengerName: string; type: PassengerType; passportNo?: string; passportExpiry?: string; nationality?: string; cnic?: string; phone?: string; passportWarning: boolean }
export interface Passenger { id?: string; fullName: string; type: PassengerType; passportNo?: string; passportExpiry?: string; nationality?: string; cnic?: string; dateOfBirth?: string; phone?: string }
export interface BookingItem {
  id: string; type: BookingItemType; description: string; serviceDate?: string; supplierId?: string; supplierName?: string; quantity: number
  unitCost: number; unitPrice: number; costTotal: number; sellTotal: number; taxRateId?: string; tourDepartureId?: string; departureCode?: string
  adults: number; children: number; airline?: string; pnr?: string; ticketNumber?: string; route?: string; country?: string; visaStatus?: VisaStatus
  passengerId?: string; passengerName?: string; billId?: string
}
export interface Booking {
  id: string; number: string; entityId: string; entityName: string; customerId: string; customerName: string; contactPhone?: string; travelDate?: string
  status: TravelBookingStatus; notes?: string; invoiceId?: string; invoiceNumber?: string; passengers: Passenger[]; items: BookingItem[]
  deposits: { id: string; date: string; amount: number; bankAccountName: string; reference?: string; applied: boolean }[]
  sellTotal: number; costTotal: number; margin: number; depositsTotal: number; warnings: string[]; createdByName?: string
}
export interface BookingListItem { id: string; number: string; customerName: string; travelDate?: string; passengers: number; services: string; status: TravelBookingStatus; sellTotal: number; margin: number; warnings: number }
export interface VisaRow { bookingId: string; bookingNumber: string; customerName: string; passengerName?: string; country?: string; status: VisaStatus; travelDate?: string }
export interface TravelDashboard { openBookings: number; confirmedThisMonth: number; salesThisMonth: number; marginThisMonth: number; upcomingDepartures: DepartureListItem[]; upcomingTravel: BookingListItem[]; pendingVisas: VisaRow[]; passportWarnings: ManifestRow[] }

export const BOOKING_COLORS: Record<TravelBookingStatus, string> = { Quotation: 'gold', Confirmed: 'blue', Invoiced: 'green', Cancelled: 'red' }
export const DEP_COLORS: Record<DepartureStatus, string> = { Open: 'green', Closed: 'orange', Departed: 'blue', Completed: 'default', Cancelled: 'red' }
export const VISA_COLORS: Record<VisaStatus, string> = { NotStarted: 'default', DocumentsCollected: 'gold', Submitted: 'blue', Approved: 'green', Rejected: 'red' }
export const ITEM_TYPES: BookingItemType[] = ['TourSeats', 'Flight', 'Hotel', 'Visa', 'Transport', 'Insurance', 'Other']
export const VISA_STATUSES: VisaStatus[] = ['NotStarted', 'DocumentsCollected', 'Submitted', 'Approved', 'Rejected']
export const words = (s: string) => s === 'TourSeats' ? 'Tour seats' : s.replace(/([a-z])([A-Z])/g, '$1 $2')
