import { FilesystemItem, type FilesystemNode } from '@/components/ui/filesystem-item'

const nodes: FilesystemNode[] = [
  {
    name: 'الوثائق',
    nodes: [
      {
        name: 'العقود',
        nodes: [
          {
            name: '2025',
            nodes: [
              { name: 'عقد توريد أجهزة.pdf' },
              { name: 'عقد صيانة سنوي.pdf' },
            ],
          },
          { name: '2026', nodes: [] },
        ],
      },
      {
        name: 'المحاضر',
        nodes: [{ name: '2026', nodes: [{ name: 'محضر اجتماع اللجنة.docx' }] }],
      },
      { name: 'الصور', nodes: [] },
      { name: 'قيد المتابعة', nodes: [] },
      { name: 'ملاحظات.txt' },
    ],
  },
]

export function FilesystemItemAnimatedDemo() {
  return (
    <div className="h-[600px] w-[400px] overflow-y-auto p-4">
      <ul>
        {nodes.map((node) => (
          <FilesystemItem node={node} key={node.name} animated />
        ))}
      </ul>
    </div>
  )
}

export function FilesystemItemDemo() {
  return (
    <div className="h-[600px] w-[400px] overflow-y-auto p-4">
      <ul>
        {nodes.map((node) => (
          <FilesystemItem node={node} key={node.name} />
        ))}
      </ul>
    </div>
  )
}
