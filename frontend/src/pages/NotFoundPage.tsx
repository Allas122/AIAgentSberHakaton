import { Link } from 'react-router-dom';
import { routes } from '../routes';

export function NotFoundPage() {
  return (
    <div className="letters">
      <div className="letters__inner">
        <header className="letters__head">
          <h1 className="letters__title">Страница не найдена</h1>
          <p className="letters__sub">
            Такого раздела нет. Возможно, ссылка устарела или в адресе опечатка.
          </p>
        </header>

        <Link className="btn btn--primary" to={routes.chat}>
          Вернуться к заявкам
        </Link>
      </div>
    </div>
  );
}
