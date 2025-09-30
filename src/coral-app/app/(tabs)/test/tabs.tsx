import { Button } from '@/components/ui/button';
import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Text } from '@/components/ui/text';
import * as React from 'react';
import { View } from 'react-native';
 
export default function TabsPreview() {
  const [value, setValue] = React.useState('feedback');
  return (
    <View className="flex w-full max-w-sm flex-col gap-6">
      <Tabs value={value} onValueChange={setValue}>
        <TabsList>
          <TabsTrigger value="feedback">
            <Text>Feedback</Text>
          </TabsTrigger>
          <TabsTrigger value="survey">
            <Text>Survey</Text>
          </TabsTrigger>
        </TabsList>
 
        <TabsContent value="feedback">
          <Card>
            <CardHeader>
              <CardTitle>Feedback</CardTitle>
              <CardDescription>
                Share your thoughts with us. Click submit when you’re ready.
              </CardDescription>
            </CardHeader>
            <CardContent className="gap-6">
              <View className="gap-3">
                <Label htmlFor="tabs-demo-name">Name</Label>
                <Input id="tabs-demo-name" defaultValue="Michael Scott" />
              </View>
              <View className="gap-3">
                <Label htmlFor="tabs-demo-message">Message</Label>
                <Input id="tabs-demo-message" defaultValue="Where are the turtles?!" />
              </View>
            </CardContent>
            <CardFooter>
              <Button>
                <Text>Submit feedback</Text>
              </Button>
            </CardFooter>
          </Card>
        </TabsContent>
 
        <TabsContent value="survey">
          <Card>
            <CardHeader>
              <CardTitle>Quick Survey</CardTitle>
              <CardDescription>
                Answer a few quick questions to help improve the demo.
              </CardDescription>
            </CardHeader>
            <CardContent className="gap-6">
              <View className="gap-3">
                <Label htmlFor="tabs-demo-job-title">Job Title</Label>
                <Input id="tabs-demo-job-title" defaultValue="Regional Manager" />
              </View>
              <View className="gap-3">
                <Label htmlFor="tabs-demo-favorite">Favorite feature</Label>
                <Input id="tabs-demo-favorite" defaultValue="CLI" />
              </View>
            </CardContent>
            <CardFooter>
              <Button>
                <Text>Submit survey</Text>
              </Button>
            </CardFooter>
          </Card>
        </TabsContent>
      </Tabs>
    </View>
  );
}