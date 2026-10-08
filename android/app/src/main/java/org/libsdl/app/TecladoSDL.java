package org.libsdl.app;

/**
 * Ponte pro teclado virtual do SDL (os metodos de esconder sao do pacote).
 * Usada pelo Elifoot 98 pra abrir o teclado quando um campo do jogo ganha foco.
 */
public final class TecladoSDL {
    private TecladoSDL() {}

    public static void mostrar() {
        if (SDLActivity.mSingleton != null)
            SDLActivity.showTextInput(0, 0, 1, 1);
    }

    public static void esconder() {
        if (SDLActivity.mSingleton != null)
            SDLActivity.mSingleton.sendCommand(SDLActivity.COMMAND_TEXTEDIT_HIDE, null);
    }
}
