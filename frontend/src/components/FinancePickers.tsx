import { useQuery } from '@tanstack/react-query'
import { Select, Tag } from 'antd'
import { api } from '../api/client'
import type { Account, AccountSubType, AccountType, Contact, FinanceSettings, TaxRate, WhtRate } from '../api/finance'

/* eslint-disable react-refresh/only-export-components */
export const useAccounts = () => useQuery({ queryKey: ['fin-accounts'], queryFn: async () => (await api.get<Account[]>('/finance/accounts')).data, staleTime: 60_000 })
export const useTaxRates = () => useQuery({ queryKey: ['fin-tax-rates'], queryFn: async () => (await api.get<TaxRate[]>('/finance/tax-rates')).data, staleTime: 60_000 })
export const useFinanceSettings = () => useQuery({ queryKey: ['fin-settings'], queryFn: async () => (await api.get<FinanceSettings>('/finance/settings')).data, staleTime: 60_000 })
export const useWhtRates = () => useQuery({ queryKey: ['fin-wht-rates'], queryFn: async () => (await api.get<WhtRate[]>('/finance/withholding/rates')).data, staleTime: 60_000 })

/** Income tax withholding rate (s.153); shows the section and the rate. */
export function WhtRateSelect({ value, onChange, allowClear = true, placeholder }: { value?: string; onChange?: (v?: string) => void; allowClear?: boolean; placeholder?: string }) {
  const { data = [] } = useWhtRates()
  return <Select value={value} onChange={onChange} allowClear={allowClear} placeholder={placeholder ?? 'No withholding'} style={{ width: '100%' }} popupMatchSelectWidth={false}
    options={data.filter(r => r.isActive || r.id === value).map(r => ({ value: r.id, label: `${r.section} · ${r.name} — ${+(r.rate * 100).toFixed(2)}%` }))} />
}

export const useContacts = (filter?: { customers?: boolean; vendors?: boolean }) =>
  useQuery({ queryKey: ['fin-contacts', filter], queryFn: async () => (await api.get<Contact[]>('/finance/contacts', { params: filter })).data })

/** Posting (non-group, active) accounts, optionally limited by type or sub-type. */
export function AccountSelect({ value, onChange, types, subTypes, placeholder, allowClear, style }: {
  value?: string; onChange?: (v: string) => void; types?: AccountType[]; subTypes?: AccountSubType[]; placeholder?: string
  allowClear?: boolean; style?: React.CSSProperties
}) {
  const { data = [] } = useAccounts()
  const options = data.filter(a => !a.isGroup && a.isActive && (!types || types.includes(a.type)) && (!subTypes || subTypes.includes(a.subType)))
    .map(a => ({ value: a.id, label: `${a.code} ${a.name}${a.currency ? ` (${a.currency})` : ''}` }))
  return <Select value={value} onChange={onChange} options={options} showSearch={{ optionFilterProp: 'label' }} placeholder={placeholder ?? 'Account'}
    allowClear={allowClear} style={{ width: '100%', ...style }} popupMatchSelectWidth={false} />
}

export function TaxRateSelect({ value, onChange }: { value?: string; onChange?: (v?: string) => void }) {
  const { data = [] } = useTaxRates()
  return <Select value={value} onChange={onChange} allowClear placeholder="No tax" style={{ width: '100%' }} popupMatchSelectWidth={false}
    options={data.filter(t => t.isActive).map(t => ({ value: t.id, label: t.name }))} />
}

export function ContactSelect({ value, onChange, customers, vendors }: { value?: string; onChange?: (v: string) => void; customers?: boolean; vendors?: boolean }) {
  const { data = [] } = useContacts({ customers, vendors })
  return <Select value={value} onChange={onChange} showSearch={{ optionFilterProp: 'label' }} style={{ width: '100%' }} placeholder="Select contact"
    options={data.filter(c => c.isActive).map(c => ({ value: c.id, label: `${c.name} (${c.code})${c.currency ? ` · ${c.currency}` : ''}` }))} />
}

export const CurrencyTag = ({ currency, base }: { currency: string; base?: string }) => currency !== base ? <Tag color="purple">{currency}</Tag> : null
