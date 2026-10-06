import { useMemo } from 'react'
import { TreeSelect } from 'antd'
import { useAuth } from '../auth/AuthContext'

interface Node { id: string; parentId?: string; name: string; code: string }
export interface TreeNode { key: string; value: string; title: string; children: TreeNode[]; disabled?: boolean }

/** Builds a forest from a flat list. Nodes whose parent isn't in the list become roots. */
// eslint-disable-next-line react-refresh/only-export-components
export function buildTree<T extends Node>(items: T[], isDisabled?: (n: T) => boolean): TreeNode[] {
  const map = new Map<string, TreeNode>()
  items.forEach(i => map.set(i.id, { key: i.id, value: i.id, title: `${i.name} (${i.code})`, children: [], disabled: isDisabled?.(i) }))
  const roots: TreeNode[] = []
  items.forEach(i => {
    const node = map.get(i.id)!
    const parent = i.parentId ? map.get(i.parentId) : undefined
    if (parent) parent.children.push(node)
    else roots.push(node)
  })
  return roots
}

/** Picks an entity from those the signed-in user can see; entities lacking `permission` are greyed out. */
export default function EntityPicker({ value, onChange, permission, placeholder, exclude }: {
  value?: string; onChange?: (v: string) => void; permission?: string; placeholder?: string; exclude?: (id: string) => boolean
}) {
  const { me } = useAuth()
  const tree = useMemo(
    () => buildTree((me?.entities ?? []).filter(e => !exclude?.(e.id)),
      e => !!permission && !e.permissions.includes(permission)),
    [me, permission, exclude],
  )
  return (
    <TreeSelect
      value={value}
      onChange={onChange}
      treeData={tree}
      treeDefaultExpandAll
      showSearch={{ treeNodeFilterProp: 'title' }}
      placeholder={placeholder ?? 'Select entity'}
      style={{ width: '100%' }}
    />
  )
}
