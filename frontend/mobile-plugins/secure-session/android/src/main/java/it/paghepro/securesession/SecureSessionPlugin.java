package it.paghepro.securesession;

import android.content.Context;
import android.content.SharedPreferences;
import android.security.keystore.KeyGenParameterSpec;
import android.security.keystore.KeyProperties;
import android.util.Base64;
import com.getcapacitor.JSObject;
import com.getcapacitor.Plugin;
import com.getcapacitor.PluginCall;
import com.getcapacitor.PluginMethod;
import com.getcapacitor.annotation.CapacitorPlugin;
import java.nio.ByteBuffer;
import java.nio.charset.StandardCharsets;
import java.security.KeyStore;
import javax.crypto.Cipher;
import javax.crypto.KeyGenerator;
import javax.crypto.SecretKey;
import javax.crypto.spec.GCMParameterSpec;

@CapacitorPlugin(name = "SecureSession")
public final class SecureSessionPlugin extends Plugin {
    private static final String ALIAS = "it.paghepro.session.refresh.v1";
    private static final String PREFERENZE = "paghepro_sessione_sicura";
    private static final String VALORE = "refresh_cifrato";

    @PluginMethod
    public void set(PluginCall call) {
        String valore = call.getString("value");
        if (valore == null || valore.isBlank()) {
            call.reject("Valore sessione mancante.");
            return;
        }
        try {
            Cipher cipher = Cipher.getInstance("AES/GCM/NoPadding");
            cipher.init(Cipher.ENCRYPT_MODE, chiave());
            byte[] cifrato = cipher.doFinal(valore.getBytes(StandardCharsets.UTF_8));
            byte[] iv = cipher.getIV();
            ByteBuffer pacchetto = ByteBuffer.allocate(4 + iv.length + cifrato.length);
            pacchetto.putInt(iv.length).put(iv).put(cifrato);
            preferenze().edit().putString(
                VALORE, Base64.encodeToString(pacchetto.array(), Base64.NO_WRAP)).apply();
            call.resolve();
        } catch (Exception ex) {
            call.reject("Impossibile proteggere la sessione.", null, ex);
        }
    }

    @PluginMethod
    public void get(PluginCall call) {
        String pacchettoBase64 = preferenze().getString(VALORE, null);
        JSObject risultato = new JSObject();
        if (pacchettoBase64 == null) {
            call.resolve(risultato);
            return;
        }
        try {
            ByteBuffer pacchetto = ByteBuffer.wrap(Base64.decode(pacchettoBase64, Base64.NO_WRAP));
            int lunghezzaIv = pacchetto.getInt();
            if (lunghezzaIv < 12 || lunghezzaIv > 32 || pacchetto.remaining() <= lunghezzaIv)
                throw new IllegalStateException("Pacchetto sessione non valido");
            byte[] iv = new byte[lunghezzaIv];
            pacchetto.get(iv);
            byte[] cifrato = new byte[pacchetto.remaining()];
            pacchetto.get(cifrato);
            Cipher cipher = Cipher.getInstance("AES/GCM/NoPadding");
            cipher.init(Cipher.DECRYPT_MODE, chiave(), new GCMParameterSpec(128, iv));
            risultato.put("value", new String(cipher.doFinal(cifrato), StandardCharsets.UTF_8));
            call.resolve(risultato);
        } catch (Exception ex) {
            eliminaTutto();
            call.reject("Sessione locale non leggibile.", null, ex);
        }
    }

    @PluginMethod
    public void clear(PluginCall call) {
        eliminaTutto();
        call.resolve();
    }

    private SharedPreferences preferenze() {
        return getContext().getSharedPreferences(PREFERENZE, Context.MODE_PRIVATE);
    }

    private SecretKey chiave() throws Exception {
        KeyStore archivio = KeyStore.getInstance("AndroidKeyStore");
        archivio.load(null);
        if (archivio.containsAlias(ALIAS))
            return ((KeyStore.SecretKeyEntry) archivio.getEntry(ALIAS, null)).getSecretKey();
        KeyGenerator generatore = KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES, "AndroidKeyStore");
        generatore.init(new KeyGenParameterSpec.Builder(
            ALIAS, KeyProperties.PURPOSE_ENCRYPT | KeyProperties.PURPOSE_DECRYPT)
            .setBlockModes(KeyProperties.BLOCK_MODE_GCM)
            .setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE)
            .setRandomizedEncryptionRequired(true)
            .build());
        return generatore.generateKey();
    }

    private void eliminaTutto() {
        preferenze().edit().clear().apply();
        try {
            KeyStore archivio = KeyStore.getInstance("AndroidKeyStore");
            archivio.load(null);
            archivio.deleteEntry(ALIAS);
        } catch (Exception ignored) {
            // La rimozione del ciphertext rende comunque inutilizzabile il dato locale.
        }
    }
}
