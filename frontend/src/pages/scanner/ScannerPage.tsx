import { useTranslation } from 'react-i18next'
import ScannerSettings from '../../components/ScannerSettings'
import '../../styles/forms.css'

/**
 * الماسحات والطابعات as a section of its own.
 *
 * It moved out of Settings because it is not a setting in the same sense as the rest of that
 * screen: the others configure the institution and are an administrator's business, while this
 * one talks to the hardware attached to *this* machine — the scanner an employee reaches for
 * several times a day, and the printer their letters come out of. Burying it three clicks deep
 * under a screen most staff have no reason to open made a daily task feel administrative.
 *
 * ScannerSettings itself is unchanged and still renders as a card; only the page header lives
 * here, so the component stays reusable if it is ever embedded somewhere else again.
 */
export default function ScannerPage() {
  const { t } = useTranslation()
  return (
    <div>
      <header className="page__head">
        <div>
          <h1>{t('nav.scanner')}</h1>
        </div>
      </header>
      <ScannerSettings />
    </div>
  )
}
