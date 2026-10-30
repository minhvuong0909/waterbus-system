import { Link } from 'react-router-dom'
import { Icon } from '../../../components/ui'
import { t } from '../../../i18n'
import { SIGHTSEEING_RESULTS, PRIMARY_CTA } from '../constants'

export default function SightseeingCta() {
  return (
    <Link to={SIGHTSEEING_RESULTS} className={PRIMARY_CTA}>
      {t('See sightseeing trips')}
      <Icon name="arrow_forward" className="text-lg transition-transform group-hover:translate-x-1" />
    </Link>
  )
}
