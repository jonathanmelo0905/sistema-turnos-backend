// Crea una empresa de prueba completa usando la API (que debe estar corriendo).
// Uso: node scripts/seed-demo.mjs   (desde backend/)
process.env.NODE_TLS_REJECT_UNAUTHORIZED = '0';
const API = 'https://localhost:7122/api';
const PASSWORD = 'Demo1234*';

async function call(method, path, body, token) {
  const res = await fetch(API + path, {
    method,
    headers: { 'Content-Type': 'application/json', ...(token && { Authorization: `Bearer ${token}` }) },
    body: body ? JSON.stringify(body) : undefined
  });
  const text = await res.text();
  if (!res.ok) throw new Error(`${method} ${path} → ${res.status}: ${text}`);
  try { return JSON.parse(text); } catch { return text; }
}

const login = (email) => call('POST', '/Auth/login', { email, password: PASSWORD });

// 1. Empresa + dueño
await call('POST', '/Auth/register-business', {
  businessName: 'Barbería Demo', businessEmail: 'contacto@barberiademo.com',
  phone: '3001234567', address: 'Calle 10 #5-20',
  adminName: 'Daniela Dueña', adminEmail: 'owner@demo.com', password: PASSWORD
});
const owner = await login('owner@demo.com');
const { token, businessId } = owner;
console.log('Empresa creada', businessId);

// 2. Horario de la empresa: L-V 8-19, sábado 8-14, domingo cerrado
const days = [1, 2, 3, 4, 5, 6, 7].map(d => ({
  dayOfWeek: d, isOpen: d !== 7,
  openTime: d === 7 ? null : '08:00', closeTime: d === 7 ? null : d === 6 ? '14:00' : '19:00'
}));
await call('PUT', `/Businesses/${businessId}/schedule`, { days }, token);

// 3. Personal
const staff = [
  { name: 'Esteban Encargado', email: 'encargado@demo.com', role: 'Encargado' },
  { name: 'Laura Colaboradora', email: 'laura@demo.com', role: 'Colaborador' },
  { name: 'Mateo Colaborador', email: 'mateo@demo.com', role: 'Colaborador' }
];
for (const s of staff) await call('POST', '/Users', { businessId, ...s, password: PASSWORD }, token);
const users = Object.fromEntries(await Promise.all(staff.map(async s => [s.email, (await login(s.email)).userId])));

// Encargado: permisos de persona de confianza
for (const code of ['collaborators.manage', 'services.manage', 'stats.view', 'clients.contacts.view', 'flexibility.approve'])
  await call('POST', `/users/${users['encargado@demo.com']}/permissions/${code}`, null, token);

// 4. Servicios
const catalog = [
  ['Corte clásico', 'Corte con máquina y tijera', 30, 25000],
  ['Corte + barba', 'Corte completo y perfilado de barba', 45, 38000],
  ['Arreglo de barba', 'Perfilado y toalla caliente', 20, 15000],
  ['Tinte', 'Coloración completa', 60, 60000],
  ['Corte infantil', 'Menores de 12 años', 25, 18000]
];
const services = [];
for (const [name, description, durationMin, price] of catalog)
  services.push(await call('POST', '/Services', { businessId, name, description, durationMin, price }, token));
const sid = services.map(s => s.serviceId);

// 5. Servicios y horario de cada colaborador (con pausa de almuerzo)
const assign = {
  'laura@demo.com': [sid[0], sid[1], sid[3], sid[4]],
  'mateo@demo.com': [sid[0], sid[1], sid[2]],
  'encargado@demo.com': [sid[0], sid[2]]
};
for (const [email, ids] of Object.entries(assign)) {
  const userId = users[email];
  await call('POST', `/users/${userId}/services`, { userId, serviceIds: ids }, token);
  for (const d of [1, 2, 3, 4, 5, 6])
    await call('POST', `/users/${userId}/Schedules/schedule`, {
      userId, dayOfWeek: d, isAvailable: true,
      startTime: '08:00', endTime: d === 6 ? '14:00' : '18:00',
      breakStart: d === 6 ? null : '12:00', breakEnd: d === 6 ? null : '13:00'
    }, token);
}

// 6. Clientes
const clientsData = [
  ['Andrés Pérez', '3101112233', 'andres@correo.com'],
  ['Camila Rojas', '3112223344', 'camila@correo.com'],
  ['Felipe Gómez', '3123334455', 'felipe@correo.com'],
  ['Sofía Torres', '3134445566', null]
];
const clients = [];
for (const [name, phone, email] of clientsData)
  clients.push(await call('POST', '/Clients', { businessId, name, phone, email, notes: null }, token));
const cid = clients.map(c => c.clientId);

// 7. Turnos en los próximos días hábiles
function nextWorkday(offset) {
  const d = new Date(); d.setDate(d.getDate() + offset);
  while (d.getDay() === 0) d.setDate(d.getDate() + 1);
  return d.toISOString().slice(0, 10);
}
const at = (day, hhmm, min) => {
  const start = `${day}T${hhmm}:00`;
  const [h, m] = hhmm.split(':').map(Number);
  const end = new Date(Date.UTC(2000, 0, 1, h, m + min));
  return [start, `${day}T${end.toISOString().slice(11, 16)}:00`];
};
const plan = [
  [1, '09:00', 'laura@demo.com', 1, 0], [1, '10:00', 'laura@demo.com', 0, 1],
  [1, '09:00', 'mateo@demo.com', 2, 2], [2, '14:00', 'mateo@demo.com', 0, 3],
  [2, '15:00', 'laura@demo.com', 3, 0], [3, '08:30', 'encargado@demo.com', 0, 1]
];
let ok = 0;
for (const [off, hhmm, email, si, ci] of plan) {
  const [dateTimeStart, dateTimeEnd] = at(nextWorkday(off), hhmm, catalog[si][2]);
  try {
    await call('POST', '/Appointments', {
      businessId, clientId: cid[ci], userId: users[email], serviceId: sid[si], dateTimeStart, dateTimeEnd
    }, token);
    ok++;
  } catch (e) { console.warn('Turno omitido:', e.message); }
}
console.log(`Listo: 4 usuarios, ${services.length} servicios, ${clients.length} clientes, ${ok} turnos. Contraseña: ${PASSWORD}`);
