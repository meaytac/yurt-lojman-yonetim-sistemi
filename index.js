const { Client, LocalAuth } = require('whatsapp-web.js');
const qrcode = require('qrcode-terminal');

const client = new Client({
    authStrategy: new LocalAuth(),
    puppeteer: {
        // Bilgisayarındaki Microsoft Edge'in standart dosya yolu
        executablePath: 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe',
    }
});

// QR Kod oluştuğunda
client.on('qr', (qr) => {
    qrcode.generate(qr, { small: true });
    console.log('Lütfen yukarıdaki QR kodu WhatsApp uygulamanızdan okutun.');
});

// Başarılı bağlantı
client.on('ready', () => {
    console.log('Bot başarıyla bağlandı ve kullanıma hazır!');
});

// Mesaj dinleyici
client.on('message_create', message => {
    console.log(`Gelen mesaj: ${message.body}`);

    const komut = message.body.toLowerCase().trim();

    // Merhaba mesajı
    if (komut === 'merhaba') {
        message.reply('Merhaba! Yurt otomasyon botuna hoş geldin. Duyuruları görmek için *duyurular* yazabilirsin.');
    }

    // Duyuruları listeleme komutu
    else if (komut === 'duyurular' || komut === '!duyurular') {
        const duyuruMetni =
            "📢 *GÜNCEL YURT DUYURULARI* 📢\n\n" +
            "1️⃣ *Yemekhane Saatleri:* Hafta içi kahvaltı 07:30 - 09:30 arasıdır.\n" +
            "2️⃣ *İnternet Bakımı:* Cuma gecesi 02:00 - 04:00 arasında altyapı çalışması olacaktır.\n" +
            "3️⃣ *İzin Dilekçeleri:* Hafta sonu izinleri için son başvuru Perşembe 17:00'dir.";

        message.reply(duyuruMetni);
    }

    else if (komut === '!ping') {
        message.sendMessage(message.from, 'pong');
    }
}); client.on('ready', () => {
    console.log('✅ Bot başarıyla bağlandı ve şu an aktif!');
});

client.initialize();