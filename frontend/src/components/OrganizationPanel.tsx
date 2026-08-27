import { useCallback, useEffect, useState } from 'react';
import { api } from '../api/client';
import type { OrganizationProfile } from '../api/types';
import { EMPTY_ORGANIZATION_PROFILE } from '../api/types';
import { IconSend } from './Icons';

interface Props {
  notify: (kind: 'info' | 'success' | 'error', text: string) => void;
}

type Field = {
  key: keyof Omit<OrganizationProfile, 'updatedAt'>;
  label: string;
  placeholder?: string;
  wide?: boolean;
};

const ORGANIZATION: Field[] = [
  { key: 'fullName', label: 'Полное наименование', placeholder: 'федеральное государственное бюджетное образовательное учреждение высшего образования «…»', wide: true },
  { key: 'shortName', label: 'Краткое наименование', placeholder: 'ФГБОУ ВО «…»' },
  { key: 'address', label: 'Адрес', placeholder: 'К. Маркса ул., д. 1, г. Иркутск, 664003', wide: true },
  { key: 'phone', label: 'Телефон', placeholder: '(3952) 521-900' },
  { key: 'fax', label: 'Факс', placeholder: '(3952) 24-22-38' },
  { key: 'email', label: 'Эл. почта организации', placeholder: 'rector@example.ru' },
  { key: 'website', label: 'Сайт', placeholder: 'www.example.ru' },
];

const REQUISITES: Field[] = [
  { key: 'okpo', label: 'ОКПО' },
  { key: 'ogrn', label: 'ОГРН' },
  { key: 'inn', label: 'ИНН' },
  { key: 'kpp', label: 'КПП' },
];

const SIGNER: Field[] = [
  { key: 'signerPosition', label: 'Должность подписанта', placeholder: 'Ректор, профессор' },
  { key: 'signerName', label: 'Подпись (ФИО)', placeholder: 'А.Ф. Шмидт' },
];

const EXECUTOR: Field[] = [
  { key: 'executorName', label: 'ФИО', placeholder: 'С.М. Лимарь' },
  { key: 'executorPhone', label: 'Телефон', placeholder: '8 (3952) 521-553' },
];

const CONTACT: Field[] = [
  { key: 'contactName', label: 'ФИО', placeholder: 'Манзула Александр Евгеньевич' },
  { key: 'contactPosition', label: 'Должность', placeholder: 'проректор по молодёжной политике' },
  { key: 'contactPhone', label: 'Телефон', placeholder: '+7 902 761-45-76' },
  { key: 'contactEmail', label: 'Эл. почта', placeholder: 'promol@example.ru' },
];

export function OrganizationPanel({ notify }: Props) {
  const [profile, setProfile] = useState<OrganizationProfile>(EMPTY_ORGANIZATION_PROFILE);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [dirty, setDirty] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setProfile(await api.organizationProfile());
      setDirty(false);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Не удалось загрузить карточку организации');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const set = (key: Field['key'], value: string) => {
    setProfile((current) => ({ ...current, [key]: value }));
    setDirty(true);
  };

  const save = async () => {
    if (saving) return;

    setSaving(true);
    setError(null);
    try {
      setProfile(await api.saveOrganizationProfile(profile));
      setDirty(false);
      notify('success', 'Карточка организации сохранена');
    } catch (e) {
      const text = e instanceof Error ? e.message : 'Не удалось сохранить карточку';
      setError(text);
      notify('error', text);
    } finally {
      setSaving(false);
    }
  };

  const renderFields = (fields: Field[], compact = false) => (
    <div className={compact ? 'org__grid org__grid--compact' : 'org__grid'}>
      {fields.map((field) => (
        <label
          key={field.key}
          className={field.wide ? 'org__field org__field--wide' : 'org__field'}
        >
          <span className="label">{field.label}</span>
          <input
            className="org__input"
            value={profile[field.key]}
            disabled={loading || saving}
            placeholder={field.placeholder}
            title={field.placeholder}
            onChange={(e) => set(field.key, e.target.value)}
          />
        </label>
      ))}
    </div>
  );

  return (
    <div className="letters">
      <div className="letters__inner letters__inner--wide">
        <header className="letters__head">
          <h1 className="letters__title">Организация и подпись</h1>
          <p className="letters__sub">
            Заполните один раз — дальше эти данные сами подставляются в письма: подпись,
            контактное лицо, реквизиты. Без них система берёт контакты из входящего письма,
            а там они принадлежат отправителю.
          </p>
        </header>

        {loading && <span className="note">Загружаю…</span>}

        <section className="letters__block org__section">
          <span className="label">Организация</span>
          {renderFields(ORGANIZATION)}
        </section>

        <section className="letters__block org__section">
          <span className="label">Реквизиты</span>
          <span className="note">
            Идут на бланк письма. В блок «Контактное лицо» они не подставляются никогда.
          </span>
          {renderFields(REQUISITES, true)}
        </section>

        <section className="letters__block org__section">
          <span className="label">Кто подписывает письма</span>
          {renderFields(SIGNER)}
        </section>

        <section className="letters__block org__section">
          <span className="label">Контактное лицо по умолчанию</span>
          <span className="note">
            Именно эти контакты уходят в блок «Контактное лицо» ответа.
          </span>
          {renderFields(CONTACT)}
        </section>

        <section className="letters__block org__section">
          <span className="label">Исполнитель</span>
          <span className="note">
            Строка внизу бланка: «Исп. …», «Тел. …». Это тот, кто готовил письмо, —
            он может не совпадать с контактным лицом из текста.
          </span>
          {renderFields(EXECUTOR, true)}
        </section>

        {error && (
          <span className="chip chip--warn">
            <span className="chip__text">{error}</span>
          </span>
        )}

        <div className="letters__actions">
          <button className="btn btn--primary" onClick={save} disabled={loading || saving || !dirty}>
            <IconSend size={15} />
            {saving ? 'Сохраняю…' : 'Сохранить'}
          </button>
          {profile.updatedAt && (
            <span className="note">
              Обновлено: {new Date(profile.updatedAt).toLocaleString('ru-RU')}
            </span>
          )}
        </div>
      </div>
    </div>
  );
}
