import { useState } from 'react'
import { App, Button } from 'antd'
import { FileExcelOutlined } from '@ant-design/icons'
import { api, errorMessage } from '../api/client'
import type { PagedResult } from '../api/types'

/* eslint-disable react-refresh/only-export-components */

export interface ExportColumn<T> {
  title: string
  value: (row: T) => string | number | boolean | null | undefined
  /** money: 2 decimals with lakh grouping; date: ISO yyyy-mm-dd strings become Excel dates. */
  type?: 'text' | 'number' | 'money' | 'date'
  width?: number
}

/** Writes rows to a real .xlsx (bold frozen header, typed numbers and dates, totals for money columns) and downloads it. */
export async function exportToExcel<T>(fileName: string, sheetName: string, columns: ExportColumn<T>[], rows: T[], title?: string) {
  const { Workbook } = await import('exceljs') // loaded only when someone exports
  const wb = new Workbook()
  wb.creator = 'ERPOS'
  const ws = wb.addWorksheet(sheetName.slice(0, 31))
  let r = 1
  if (title) {
    ws.getCell(1, 1).value = title
    ws.getCell(1, 1).font = { bold: true, size: 13 }
    r = 3
  }
  columns.forEach((c, i) => {
    const cell = ws.getCell(r, i + 1)
    cell.value = c.title
    cell.font = { bold: true }
    cell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: 'FFE8ECF8' } }
    ws.getColumn(i + 1).width = c.width ?? Math.max(12, Math.min(48, c.title.length + 4))
    if (c.type === 'money') ws.getColumn(i + 1).numFmt = '#,##,##0.00'
    if (c.type === 'number') ws.getColumn(i + 1).numFmt = '#,##0.##'
    if (c.type === 'date') ws.getColumn(i + 1).numFmt = 'dd mmm yyyy'
  })
  ws.views = [{ state: 'frozen', ySplit: r }]
  const first = r + 1
  for (const row of rows) {
    r++
    columns.forEach((c, i) => {
      const v = c.value(row)
      ws.getCell(r, i + 1).value = v == null || v === '' ? null
        : c.type === 'date' && typeof v === 'string' ? new Date(`${v.slice(0, 10)}T00:00:00Z`)
          : (c.type === 'money' || c.type === 'number') && typeof v !== 'number' ? Number(v) : v as string | number | boolean
    })
  }
  if (rows.length && columns.some(c => c.type === 'money')) {
    r++
    ws.getCell(r, 1).value = 'Total'
    ws.getRow(r).font = { bold: true }
    columns.forEach((c, i) => {
      if (c.type !== 'money' || i === 0) return
      const col = ws.getColumn(i + 1).letter
      ws.getCell(r, i + 1).value = { formula: `SUM(${col}${first}:${col}${r - 1})` }
    })
  }
  const buffer = await wb.xlsx.writeBuffer()
  const url = URL.createObjectURL(new Blob([buffer], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' }))
  const a = document.createElement('a')
  a.href = url
  a.download = `${fileName}.xlsx`
  a.click()
  setTimeout(() => URL.revokeObjectURL(url), 1000)
}

/** Loads every page of a paged list endpoint (up to `max` rows) for export. */
export async function fetchAllPages<T>(url: string, params: Record<string, unknown> = {}, max = 10000): Promise<T[]> {
  const out: T[] = []
  for (let page = 1; out.length < max; page++) {
    const { data } = await api.get<PagedResult<T>>(url, { params: { ...params, page, pageSize: 200 } })
    out.push(...data.items)
    if (data.items.length < 200 || out.length >= data.total) break
  }
  return out
}

/** "Export to Excel" button: rows come from the caller (already loaded, or fetched in full on click). */
export default function ExportButton<T>({ fileName, sheetName, title, columns, rows, disabled }: {
  fileName: string; sheetName?: string; title?: string; columns: ExportColumn<T>[]; rows: T[] | (() => Promise<T[]>); disabled?: boolean
}) {
  const { message } = App.useApp()
  const [busy, setBusy] = useState(false)
  const run = async () => {
    setBusy(true)
    try {
      const data = typeof rows === 'function' ? await rows() : rows
      if (!data.length) { message.info('Nothing to export.'); return }
      await exportToExcel(fileName, sheetName ?? fileName, columns, data, title)
    } catch (e) { message.error(errorMessage(e)) } finally { setBusy(false) }
  }
  return <Button icon={<FileExcelOutlined />} loading={busy} disabled={disabled} onClick={run}>Excel</Button>
}
