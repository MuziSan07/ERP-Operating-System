import { useQuery } from '@tanstack/react-query'
import { Card, Col, Empty, Flex, Row, Statistic, Tag, Typography } from 'antd'
import { ApartmentOutlined, SafetyOutlined, TeamOutlined, UserOutlined } from '@ant-design/icons'
import { api } from '../api/client'
import type { Dashboard } from '../api/types'
import { useAuth } from '../auth/AuthContext'

export default function DashboardPage() {
  const { me } = useAuth()
  const { data, isLoading } = useQuery({
    queryKey: ['dashboard'],
    queryFn: async () => (await api.get<Dashboard>('/dashboard')).data,
  })

  const stats = [
    { title: 'Entities', value: data?.entities, icon: <ApartmentOutlined /> },
    { title: 'Users', value: data?.users, icon: <TeamOutlined /> },
    { title: 'Active users', value: data?.activeUsers, icon: <UserOutlined /> },
    { title: 'Roles', value: data?.roles, icon: <SafetyOutlined /> },
  ]

  return (
    <>
      <div className="page-header">
        <Typography.Title level={2}>Welcome, {me?.user.fullName.split(' ')[0]}</Typography.Title>
      </div>
      <Row gutter={[16, 16]}>
        {stats.map(s => (
          <Col key={s.title} xs={12} md={6}>
            <Card loading={isLoading}><Statistic title={s.title} value={s.value ?? 0} prefix={s.icon} /></Card>
          </Col>
        ))}
        <Col xs={24} md={12}>
          <Card title="Users by type" loading={isLoading}>
            <Breakdown data={data?.usersByType} color="blue" />
          </Card>
        </Col>
        <Col xs={24} md={12}>
          <Card title="Entities by industry" loading={isLoading}>
            <Breakdown data={data?.entitiesByIndustry} color="geekblue" />
          </Card>
        </Col>
      </Row>
    </>
  )
}

function Breakdown({ data, color }: { data?: Record<string, number>; color: string }) {
  const rows = Object.entries(data ?? {}).sort((a, b) => b[1] - a[1])
  if (!rows.length) return <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} />
  return (
    <Flex vertical gap={10}>
      {rows.map(([k, v]) => (
        <Flex key={k} justify="space-between" align="center"><Tag color={color}>{k}</Tag><b>{v}</b></Flex>
      ))}
    </Flex>
  )
}
