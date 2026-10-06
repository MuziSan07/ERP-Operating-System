import { useParams } from 'react-router-dom'
import { Card, Result, Space, Tag, Typography } from 'antd'
import { useAuth } from '../auth/AuthContext'
import { BUSINESS_MODULES } from '../layout/AppLayout'

/** Shown for business modules whose screens are not built yet; lists what the user is permitted to do there. */
export default function ModulePlaceholder() {
  const { module } = useParams()
  const { me } = useAuth()
  const def = BUSINESS_MODULES.find(m => m.code === module)
  const perms = [...new Set(me?.entities.flatMap(e => e.permissions).filter(p => p.startsWith(`${module}.`)))].sort()
  const entities = me?.entities.filter(e => e.permissions.some(p => p.startsWith(`${module}.`))) ?? []

  return (
    <>
      <div className="page-header"><Typography.Title level={2}>{def?.label ?? module}</Typography.Title></div>
      <Card>
        <Result status="info" icon={def?.icon} title={`${def?.label} screens are coming in the next phase`}
          subTitle="The module is enabled and your permissions are already enforced by the API." />
        <Typography.Title level={5}>Available at</Typography.Title>
        <Space wrap style={{ marginBottom: 16 }}>{entities.map(e => <Tag key={e.id}>{e.name}</Tag>)}</Space>
        <Typography.Title level={5}>Your permissions</Typography.Title>
        <Space size={[4, 4]} wrap>{perms.map(p => <Tag key={p} color="blue">{p}</Tag>)}</Space>
      </Card>
    </>
  )
}
