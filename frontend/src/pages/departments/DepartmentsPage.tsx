import { auth } from '../../lib/auth'
import OrganizationSettings from '../../components/OrganizationSettings'
import DocumentTypesSettings from '../../components/DocumentTypesSettings'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '../../components/ui/tabs'

/** الأقسام — organizational units, their staff, and each department's document types.
 * Moved out of Settings into its own top-level section. */
export default function DepartmentsPage() {
  return (
    <div>
      <Tabs defaultValue="units" dir="rtl">
        <TabsList>
          <TabsTrigger value="units">الدوائر والمناصب</TabsTrigger>
          {auth.hasPermission('Documents.View') && <TabsTrigger value="docTypes">أنواع الوثائق</TabsTrigger>}
        </TabsList>
        <TabsContent value="units">
          <OrganizationSettings />
        </TabsContent>
        {auth.hasPermission('Documents.View') && (
          <TabsContent value="docTypes">
            <DocumentTypesSettings />
          </TabsContent>
        )}
      </Tabs>
    </div>
  )
}
