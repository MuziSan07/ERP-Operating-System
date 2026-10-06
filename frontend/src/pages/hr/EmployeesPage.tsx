import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import {
  App, Button, Card, Checkbox, Col, DatePicker, Divider, Drawer, Form, Input, InputNumber, Row, Select, Table, Tag, Typography,
} from 'antd'
import { PlusOutlined } from '@ant-design/icons'
import dayjs from 'dayjs'
import { api, errorMessage } from '../../api/client'
import type { PagedResult, Role } from '../../api/types'
import {
  EMPLOYEE_STATUSES, EMPLOYMENT_TYPES, fmtDate, toIsoDate, type Department, type Designation, type Employee,
  type EmployeeData, type EmployeeListItem, type EmployeeStatus,
} from '../../api/hr'
import { P, useAuth } from '../../auth/AuthContext'
import EntityPicker from '../../components/EntityPicker'

const STATUS_COLOR: Record<EmployeeStatus, string> = { Active: 'green', Resigned: 'default', Terminated: 'red', Retired: 'blue' }

export default function EmployeesPage() {
  const { can } = useAuth()
  const navigate = useNavigate()
  const [entityId, setEntityId] = useState<string>()
  const [status, setStatus] = useState<EmployeeStatus | undefined>('Active')
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [creating, setCreating] = useState(false)

  const { data, isFetching } = useQuery({
    queryKey: ['employees', entityId, status, search, page],
    queryFn: async () => (await api.get<PagedResult<EmployeeListItem>>('/hr/employees', { params: { entityId, status, search, page, pageSize: 25 } })).data,
  })

  return (
    <>
      <div className="page-header">
        <Typography.Title level={2}>Employees</Typography.Title>
        {can(P.employeesCreate) && <Button type="primary" icon={<PlusOutlined />} onClick={() => setCreating(true)}>New employee</Button>}
      </div>
      <Card>
        <Row gutter={[12, 12]} style={{ marginBottom: 16 }}>
          <Col xs={24} md={8}><EntityPicker value={entityId} onChange={v => { setEntityId(v); setPage(1) }} placeholder="All entities" /></Col>
          <Col xs={12} md={5}>
            <Select allowClear placeholder="Any status" value={status} style={{ width: '100%' }}
              onChange={v => { setStatus(v); setPage(1) }} options={EMPLOYEE_STATUSES.map(s => ({ value: s, label: s }))} />
          </Col>
          <Col xs={12} md={8}><Input.Search allowClear placeholder="Name, code, email or CNIC" onSearch={v => { setSearch(v); setPage(1) }} /></Col>
          {entityId && <Col><Button onClick={() => setEntityId(undefined)}>Clear</Button></Col>}
        </Row>
        <Table<EmployeeListItem> rowKey="id" loading={isFetching} dataSource={data?.items} scroll={{ x: 900 }}
          onRow={r => ({ onClick: () => navigate(`/hr/employees/${r.id}`), style: { cursor: 'pointer' } })}
          pagination={{ current: page, pageSize: 25, total: data?.total, onChange: setPage, showSizeChanger: false }}
          columns={[
            { title: 'Code', dataIndex: 'employeeCode', width: 110 },
            { title: 'Name', render: (_, e) => <><b>{e.fullName}</b><div><Typography.Text type="secondary">{e.email}</Typography.Text></div></> },
            { title: 'Designation', dataIndex: 'designation' },
            { title: 'Department', dataIndex: 'department' },
            { title: 'Entity', dataIndex: 'entityName' },
            { title: 'Manager', dataIndex: 'managerName' },
            { title: 'Joined', dataIndex: 'joinDate', render: fmtDate },
            { title: 'Status', dataIndex: 'status', render: (s: EmployeeStatus) => <Tag color={STATUS_COLOR[s]}>{s}</Tag> },
          ]} />
      </Card>
      {creating && <EmployeeDrawer onClose={() => setCreating(false)} onSaved={id => navigate(`/hr/employees/${id}`)} />}
    </>
  )
}

/** Create (with login) or edit an employee. */
export function EmployeeDrawer({ employee, onClose, onSaved }: { employee?: Employee; onClose: () => void; onSaved?: (id: string) => void }) {
  const [form] = Form.useForm()
  const qc = useQueryClient()
  const { message } = App.useApp()
  const { me, can } = useAuth()
  const [busy, setBusy] = useState(false)
  const entityId = Form.useWatch(['data', 'entityId'], form) as string | undefined
  const status = Form.useWatch(['data', 'status'], form) as EmployeeStatus | undefined

  const { data: departments = [] } = useQuery({ queryKey: ['departments'], queryFn: async () => (await api.get<Department[]>('/hr/departments')).data })
  const { data: designations = [] } = useQuery({ queryKey: ['designations'], queryFn: async () => (await api.get<Designation[]>('/hr/designations')).data })
  const { data: managers } = useQuery({
    queryKey: ['employees', 'pick'], queryFn: async () => (await api.get<PagedResult<EmployeeListItem>>('/hr/employees', { params: { status: 'Active', pageSize: 500 } })).data,
  })
  const { data: roles = [] } = useQuery({ queryKey: ['roles'], queryFn: async () => (await api.get<Role[]>('/roles')).data, enabled: !employee && can(P.usersAssign) })

  const userTypes = me?.user.userType === 'SuperAdmin' || me?.user.userType === 'Admin' ? ['Employee', 'Manager', 'Admin'] : ['Employee']
  const dateFields = ['joinDate', 'confirmationDate', 'exitDate', 'dateOfBirth'] as const

  const initial = employee
    ? { data: { ...employee, ...Object.fromEntries(dateFields.map(f => [f, employee[f] ? dayjs(employee[f]) : undefined])) } }
    : { data: { employmentType: 'Permanent', status: 'Active', joinDate: dayjs(), eobiMember: true, entityId: me?.user.primaryEntityId }, userType: 'Employee' }

  const submit = async () => {
    const v = await form.validateFields()
    const data: EmployeeData = { ...v.data }
    dateFields.forEach(f => { (data as unknown as Record<string, unknown>)[f] = toIsoDate(v.data[f]) })
    setBusy(true)
    try {
      const res = employee
        ? await api.put<Employee>(`/hr/employees/${employee.id}`, data)
        : await api.post<Employee>('/hr/employees', { data, email: v.email, password: v.password, userType: v.userType, roleId: v.roleId, monthlyGross: v.monthlyGross })
      message.success(employee ? 'Employee updated' : 'Employee created with a login')
      await qc.invalidateQueries({ queryKey: ['employees'] })
      await qc.invalidateQueries({ queryKey: ['employee', res.data.id] })
      onSaved?.(res.data.id)
      onClose()
    } catch (e) {
      message.error(errorMessage(e))
    } finally {
      setBusy(false)
    }
  }

  const col = (span = 12) => ({ xs: 24, sm: span })
  return (
    <Drawer open onClose={onClose} size={760} title={employee ? `Edit ${employee.fullName}` : 'New employee'}
      extra={<Button type="primary" loading={busy} onClick={submit}>Save</Button>}>
      <Form form={form} layout="vertical" initialValues={initial}>
        <Divider titlePlacement="start" plain style={{ marginTop: 0 }}>Job</Divider>
        <Row gutter={12}>
          <Col {...col(8)}><Form.Item name={['data', 'employeeCode']} label="Employee code" rules={[{ required: true, pattern: /^[A-Za-z0-9_-]+$/ }]}><Input /></Form.Item></Col>
          <Col {...col(16)}><Form.Item name={['data', 'fullName']} label="Full name" rules={[{ required: true }]}><Input /></Form.Item></Col>
          <Col {...col()}><Form.Item name={['data', 'entityId']} label="Entity" rules={[{ required: true }]}><EntityPicker permission={employee ? P.employeesEdit : P.employeesCreate} /></Form.Item></Col>
          <Col {...col()}>
            <Form.Item name={['data', 'departmentId']} label="Department">
              <Select allowClear options={departments.filter(d => !entityId || d.entityId === entityId).map(d => ({ value: d.id, label: `${d.name} (${d.entityName})` }))} />
            </Form.Item>
          </Col>
          <Col {...col()}><Form.Item name={['data', 'designationId']} label="Designation"><Select allowClear showSearch={{ optionFilterProp: 'label' }} options={designations.map(d => ({ value: d.id, label: d.title }))} /></Form.Item></Col>
          <Col {...col()}>
            <Form.Item name={['data', 'managerId']} label="Line manager" extra="First approver of this person's leave.">
              <Select allowClear showSearch={{ optionFilterProp: 'label' }}
                options={managers?.items.filter(m => m.id !== employee?.id).map(m => ({ value: m.id, label: `${m.fullName} (${m.employeeCode})` }))} />
            </Form.Item>
          </Col>
          <Col {...col(8)}><Form.Item name={['data', 'employmentType']} label="Employment type"><Select options={EMPLOYMENT_TYPES.map(t => ({ value: t, label: t }))} /></Form.Item></Col>
          <Col {...col(8)}><Form.Item name={['data', 'joinDate']} label="Joining date" rules={[{ required: true }]}><DatePicker style={{ width: '100%' }} format="DD MMM YYYY" /></Form.Item></Col>
          <Col {...col(8)}><Form.Item name={['data', 'confirmationDate']} label="Confirmation date"><DatePicker style={{ width: '100%' }} format="DD MMM YYYY" /></Form.Item></Col>
          <Col {...col(8)}><Form.Item name={['data', 'status']} label="Status"><Select options={EMPLOYEE_STATUSES.map(s => ({ value: s, label: s }))} /></Form.Item></Col>
          {status && status !== 'Active' && (
            <Col {...col(8)}><Form.Item name={['data', 'exitDate']} label="Exit date" rules={[{ required: true }]} extra="Login is disabled."><DatePicker style={{ width: '100%' }} format="DD MMM YYYY" /></Form.Item></Col>
          )}
        </Row>

        {!employee && (
          <>
            <Divider titlePlacement="start" plain>Login</Divider>
            <Row gutter={12}>
              <Col {...col()}><Form.Item name="email" label="Email (login)" rules={[{ required: true, type: 'email' }]}><Input /></Form.Item></Col>
              <Col {...col()}><Form.Item name="password" label="Initial password" rules={[{ required: true, min: 8 }]} extra="8+ characters with letters and digits."><Input.Password autoComplete="new-password" /></Form.Item></Col>
              <Col {...col()}><Form.Item name="userType" label="User type"><Select options={userTypes.map(t => ({ value: t, label: t }))} /></Form.Item></Col>
              {can(P.usersAssign) && (
                <Col {...col()}>
                  <Form.Item name="roleId" label="Role" extra="Default: the Employee self-service role.">
                    <Select allowClear placeholder="Employee (default)" options={roles.map(r => ({ value: r.id, label: r.name }))} />
                  </Form.Item>
                </Col>
              )}
              {can(P.salaryCreate) && (
                <Col {...col()}>
                  <Form.Item name="monthlyGross" label="Monthly gross salary (PKR)" extra="Split into Basic, House rent, Medical and Utilities. You can edit it afterwards.">
                    <InputNumber min={0} step={5000} style={{ width: '100%' }} formatter={v => `${v}`.replace(/\B(?=(\d{3})+(?!\d))/g, ',')} />
                  </Form.Item>
                </Col>
              )}
            </Row>
          </>
        )}

        <Divider titlePlacement="start" plain>Personal</Divider>
        <Row gutter={12}>
          <Col {...col()}><Form.Item name={['data', 'fatherName']} label="Father's name"><Input /></Form.Item></Col>
          <Col {...col()}><Form.Item name={['data', 'cnic']} label="CNIC" rules={[{ pattern: /^\d{5}-?\d{7}-?\d$/, message: '13 digits, e.g. 12345-1234567-1' }]}><Input placeholder="12345-1234567-1" /></Form.Item></Col>
          <Col {...col(8)}><Form.Item name={['data', 'gender']} label="Gender"><Select allowClear options={['Male', 'Female', 'Other'].map(g => ({ value: g, label: g }))} /></Form.Item></Col>
          <Col {...col(8)}><Form.Item name={['data', 'dateOfBirth']} label="Date of birth"><DatePicker style={{ width: '100%' }} format="DD MMM YYYY" /></Form.Item></Col>
          <Col {...col(8)}><Form.Item name={['data', 'phone']} label="Phone"><Input /></Form.Item></Col>
          <Col {...col(16)}><Form.Item name={['data', 'address']} label="Address"><Input /></Form.Item></Col>
          <Col {...col(8)}><Form.Item name={['data', 'city']} label="City"><Input /></Form.Item></Col>
          <Col {...col()}><Form.Item name={['data', 'emergencyContactName']} label="Emergency contact"><Input /></Form.Item></Col>
          <Col {...col()}><Form.Item name={['data', 'emergencyContactPhone']} label="Emergency phone"><Input /></Form.Item></Col>
        </Row>

        <Divider titlePlacement="start" plain>Payroll & statutory</Divider>
        <Row gutter={12}>
          <Col {...col()}><Form.Item name={['data', 'bankName']} label="Bank"><Input /></Form.Item></Col>
          <Col {...col()}><Form.Item name={['data', 'bankAccountTitle']} label="Account title"><Input /></Form.Item></Col>
          <Col {...col()}><Form.Item name={['data', 'iban']} label="IBAN" rules={[{ pattern: /^PK\d{2}[A-Z]{4}[0-9A-Z]{16}$/i, message: 'Pakistani IBAN: PK + 22 characters', transform: (v?: string) => v?.replace(/\s/g, '') }]}><Input placeholder="PK36SCBL0000001123456702" /></Form.Item></Col>
          <Col {...col()}><Form.Item name={['data', 'ntn']} label="NTN"><Input /></Form.Item></Col>
          <Col {...col()}><Form.Item name={['data', 'eobiNumber']} label="EOBI number"><Input /></Form.Item></Col>
          <Col span={24}>
            <Form.Item name={['data', 'eobiMember']} valuePropName="checked" noStyle><Checkbox>EOBI member</Checkbox></Form.Item>
            <Form.Item name={['data', 'providentFundMember']} valuePropName="checked" noStyle><Checkbox>Provident fund member</Checkbox></Form.Item>
            <Form.Item name={['data', 'socialSecurityMember']} valuePropName="checked" noStyle><Checkbox>Social security member</Checkbox></Form.Item>
          </Col>
        </Row>
      </Form>
    </Drawer>
  )
}
