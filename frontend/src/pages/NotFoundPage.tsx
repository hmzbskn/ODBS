import { Link } from 'react-router-dom'
import { EmptyState } from '../components/Feedback'

export function NotFoundPage() {
  return (
    <div className="card">
      <EmptyState title="Sayfa bulunamadı">
        Aradığınız sayfa yok ya da taşınmış olabilir. <Link to="/">Genel bakışa dön</Link>
      </EmptyState>
    </div>
  )
}
