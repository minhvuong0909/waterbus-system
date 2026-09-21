import { Pressable, Text } from "react-native";

export default function Button({
  title,
  onPress,
}: {
  title: string;
  onPress: () => void;
}) {
  return (
    <Pressable
      onPress={onPress}
      style={{
        padding: 12,
        backgroundColor: "#ff6b00",
        borderRadius: 10,
      }}
    >
      <Text style={{ color: "#fff", textAlign: "center" }}>{title}</Text>
    </Pressable>
  );
}
