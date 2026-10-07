import { useQuery, useQueryClient } from '@tanstack/react-query'
import { App, Button, Empty, Input, Popconfirm, Space, Spin, Typography, Upload } from 'antd'
import { DeleteOutlined, DownloadOutlined, PaperClipOutlined, UploadOutlined } from '@ant-design/icons'
import { useState } from 'react'
import dayjs from 'dayjs'
import { api, errorMessage } from '../api/client'

export type AttachmentRecord = 'invoice' | 'bill' | 'payment' | 'journal' | 'asset' | 'employee' | 'shipment' | 'grant' | 'project' | 'beneficiary'
interface Attachment { id: string; fileName: string; contentType: string; sizeBytes: number; description?: string; uploadedAt: string; uploadedBy?: string }

const size = (b: number) => (b < 1024 * 1024 ? `${Math.max(1, Math.round(b / 1024))} KB` : `${(b / 1024 / 1024).toFixed(1)} MB`)

/** Files attached to a record. Viewing follows the record's view permission; the server enforces upload/delete rights. */
export default function Attachments({ recordType, recordId, canEdit }: { recordType: AttachmentRecord; recordId: string; canEdit: boolean }) {
  const qc = useQueryClient()
  const { message } = App.useApp()
  const [note, setNote] = useState('')
  const key = ['attachments', recordType, recordId]
  const { data = [], isLoading } = useQuery({ queryKey: key, queryFn: async () => (await api.get<Attachment[]>('/attachments', { params: { recordType, recordId } })).data })

  const download = async (a: Attachment) => {
    try {
      const res = await api.get(`/attachments/${a.id}/download`, { responseType: 'blob' })
      const url = URL.createObjectURL(res.data as Blob)
      const link = document.createElement('a')
      link.href = url
      link.download = a.fileName
      link.click()
      setTimeout(() => URL.revokeObjectURL(url), 1000)
    } catch (e) { message.error(errorMessage(e)) }
  }

  return (
    <div>
      <Typography.Title level={5}><PaperClipOutlined /> Attachments</Typography.Title>
      {canEdit && <Space.Compact style={{ width: '100%', marginBottom: 8 }}>
        <Input placeholder="Note (optional), e.g. signed delivery note" value={note} onChange={e => setNote(e.target.value)} />
        <Upload showUploadList={false} accept=".pdf,.png,.jpg,.jpeg,.webp,.xlsx,.xls,.csv,.docx,.doc,.txt"
          customRequest={async ({ file, onSuccess, onError }) => {
            const fd = new FormData()
            fd.append('recordType', recordType)
            fd.append('recordId', recordId)
            if (note) fd.append('description', note)
            fd.append('file', file as Blob)
            try { await api.post('/attachments', fd); setNote(''); message.success('File attached'); await qc.invalidateQueries({ queryKey: key }); onSuccess?.({}) }
            catch (e) { message.error(errorMessage(e)); onError?.(e as Error) }
          }}>
          <Button icon={<UploadOutlined />}>Attach file</Button>
        </Upload>
      </Space.Compact>}
      {isLoading ? <Spin size="small" /> : !data.length ? <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="No files attached" />
        : data.map(a => (
          <div key={a.id} className="attachment-row" style={{ display: 'flex', alignItems: 'center', gap: 8, padding: '6px 0', borderBottom: '1px solid rgba(5,5,5,0.06)' }}>
            <div style={{ flex: 1, minWidth: 0 }}>
              <a onClick={() => download(a)} style={{ wordBreak: 'break-all' }}>{a.fileName}</a>
              <div><Typography.Text type="secondary" style={{ fontSize: 12 }}>
                {a.description && <>{a.description} · </>}{size(a.sizeBytes)} · {a.uploadedBy ?? 'Someone'} · {dayjs(a.uploadedAt).format('DD MMM YYYY HH:mm')}
              </Typography.Text></div>
            </div>
            <Button size="small" type="text" icon={<DownloadOutlined />} onClick={() => download(a)} aria-label={`Download ${a.fileName}`} />
            {canEdit && <Popconfirm title="Remove this file?" onConfirm={async () => { try { await api.delete(`/attachments/${a.id}`); await qc.invalidateQueries({ queryKey: key }) } catch (e) { message.error(errorMessage(e)) } }}>
              <Button size="small" type="text" danger icon={<DeleteOutlined />} aria-label={`Remove ${a.fileName}`} /></Popconfirm>}
          </div>
        ))}
    </div>
  )
}
