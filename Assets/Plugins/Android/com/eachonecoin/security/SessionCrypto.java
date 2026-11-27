package com.eachonecoin.security;

import android.security.keystore.KeyGenParameterSpec;
import android.security.keystore.KeyProperties;
import java.nio.ByteBuffer;
import java.security.KeyStore;
import javax.crypto.Cipher;
import javax.crypto.KeyGenerator;
import javax.crypto.SecretKey;
import javax.crypto.spec.GCMParameterSpec;

public final class SessionCrypto {
    private static final String ALIAS = "eachonecoin.reconnect.v1";
    private static SecretKey key(boolean create) throws Exception {
        KeyStore store = KeyStore.getInstance("AndroidKeyStore");
        store.load(null);
        if (!store.containsAlias(ALIAS)) {
            if (!create) throw new IllegalStateException("Reconnect key unavailable");
            KeyGenerator generator = KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES, "AndroidKeyStore");
            generator.init(new KeyGenParameterSpec.Builder(ALIAS, KeyProperties.PURPOSE_ENCRYPT | KeyProperties.PURPOSE_DECRYPT)
                .setBlockModes(KeyProperties.BLOCK_MODE_GCM)
                .setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE).build());
            generator.generateKey();
        }
        return (SecretKey) store.getKey(ALIAS, null);
    }
    public static byte[] protect(byte[] input, boolean encrypt) throws Exception {
        Cipher cipher = Cipher.getInstance("AES/GCM/NoPadding");
        if (encrypt) {
            cipher.init(Cipher.ENCRYPT_MODE, key(true));
            byte[] iv = cipher.getIV();
            byte[] encrypted = cipher.doFinal(input);
            return ByteBuffer.allocate(2 + iv.length + encrypted.length).put((byte)1).put((byte)iv.length).put(iv).put(encrypted).array();
        }
        if (input.length < 30 || input[0] != 1 || input[1] != 12) throw new IllegalArgumentException("Invalid session data");
        byte[] iv = new byte[12];
        System.arraycopy(input, 2, iv, 0, iv.length);
        cipher.init(Cipher.DECRYPT_MODE, key(false), new GCMParameterSpec(128, iv));
        return cipher.doFinal(input, 14, input.length - 14);
    }
}
